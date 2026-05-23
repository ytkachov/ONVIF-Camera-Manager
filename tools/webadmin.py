"""Drive a camera's web admin UI through a real browser (Selenium).

Why a real browser instead of raw HTTP: vendor web admins (Hikvision et al.)
are JS apps that log in via salted/hashed challenge flows and render config
through client-side script. A browser executes that for us, so we observe and
control exactly what an operator sees — useful to cross-check the values our
Camera Manager reads/writes over ONVIF/ISAPI against the source of truth.

Design: the browser is launched ONCE (`start`) as an independent process with
a remote-debugging port. Every later command attaches to that running browser
via Chrome's debuggerAddress, performs one action, then detaches WITHOUT
closing it (stops only the chromedriver it spawned). That lets an agent drive
the session step by step across separate invocations, inspecting via
screenshots and DOM dumps between steps. `stop` kills the browser.

Credentials: resolved from the app's own store
(%APPDATA%/SeaGull/cameras.json, passwords DPAPI-encrypted) by camera name or
IP, or overridden with --user/--pass / env CAM_USER, CAM_PASS, ONVIF_PASSWORD.
Note the stored account is the ONVIF user, which on Hikvision often differs
from the web-admin login — pass --user/--pass when they diverge.

Vendor-agnostic primitives: start, stop, status, goto, info, shot, dom, js,
find, click, type, frames, login. Only `login` carries vendor heuristics, and
it is best-effort: when it can't find the form, drive manually with the
primitives.

Usage:
  python webadmin.py start --camera "Back Yard Barn"      # or --host 192.168.1.58
  python webadmin.py login --user admin --pass <pwd>
  python webadmin.py shot                                 # -> webadmin-out/shot.png
  python webadmin.py find "WDR"
  python webadmin.py js "return document.title"
  python webadmin.py stop
"""
from __future__ import annotations

import argparse
import base64
import ctypes
import ctypes.wintypes as wt
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
from selenium.webdriver.common.keys import Keys

OUT = Path(os.environ.get("WEBADMIN_OUT", Path(__file__).parent / "webadmin-out"))
STATE = OUT / ".session.json"
DEFAULT_PORT = 9333
CAMERAS_JSON = Path(os.environ.get("APPDATA", "")) / "SeaGull" / "cameras.json"
DPAPI_ENTROPY = b"OnvifManager.CameraStore.v1"


# --------------------------------------------------------------------------- #
# DPAPI: decrypt the store's CurrentUser-scoped passwords (same user only).
# --------------------------------------------------------------------------- #
class _DataBlob(ctypes.Structure):
    _fields_ = [("cbData", wt.DWORD), ("pbData", ctypes.POINTER(ctypes.c_char))]


def _to_blob(data: bytes):
    buf = ctypes.create_string_buffer(bytes(data), len(data))
    return _DataBlob(len(data), ctypes.cast(buf, ctypes.POINTER(ctypes.c_char))), buf


def dpapi_unprotect(cipher_b64: str) -> str:
    if not cipher_b64:
        return ""
    cipher = base64.b64decode(cipher_b64)
    in_blob, _k1 = _to_blob(cipher)
    ent_blob, _k2 = _to_blob(DPAPI_ENTROPY)
    out = _DataBlob()
    ok = ctypes.windll.crypt32.CryptUnprotectData(
        ctypes.byref(in_blob), None, ctypes.byref(ent_blob), None, None, 0,
        ctypes.byref(out))
    if not ok:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        return ctypes.string_at(out.pbData, out.cbData).decode("utf-8", "replace")
    finally:
        ctypes.windll.kernel32.LocalFree(out.pbData)


def resolve_camera(name_or_ip: str | None) -> dict:
    """Return {host, port, user, password, name, manufacturer, model} for the
    requested camera, decrypting the stored password. Raises if not found."""
    if not CAMERAS_JSON.exists():
        raise SystemExit(f"no camera store at {CAMERAS_JSON}")
    data = json.loads(CAMERAS_JSON.read_text(encoding="utf-8"))
    cams = data.get("Cameras", [])
    cam = None
    if name_or_ip:
        key = name_or_ip.strip().lower()
        for c in cams:
            if c.get("Name", "").lower() == key or c.get("IpAddress", "") == name_or_ip:
                cam = c
                break
        if cam is None:
            names = ", ".join(f'{c["Name"]} ({c["IpAddress"]})' for c in cams)
            raise SystemExit(f"camera '{name_or_ip}' not found. Known: {names}")
    elif len(cams) == 1:
        cam = cams[0]
    else:
        names = ", ".join(f'{c["Name"]} ({c["IpAddress"]})' for c in cams)
        raise SystemExit(f"multiple cameras; pass --camera or --host. Known: {names}")
    return {
        "host": cam["IpAddress"],
        "port": cam.get("Port", 80),
        "user": cam.get("Username", ""),
        "password": dpapi_unprotect(cam.get("PasswordCipher", "")),
        "name": cam.get("Name", ""),
        "manufacturer": cam.get("Manufacturer", ""),
        "model": cam.get("Model", ""),
    }


# --------------------------------------------------------------------------- #
# Browser lifecycle: launch once, attach per command.
# --------------------------------------------------------------------------- #
def find_browser() -> tuple[str, str]:
    """Return (path, kind) for Chrome, then Edge. kind in {chrome, edge}."""
    candidates = [
        ("chrome", os.environ.get("WEBADMIN_CHROME")),
        ("chrome", shutil.which("chrome")),
        ("chrome", r"C:\Program Files\Google\Chrome\Application\chrome.exe"),
        ("chrome", r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"),
        ("edge", shutil.which("msedge")),
        ("edge", r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"),
        ("edge", r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"),
    ]
    for kind, p in candidates:
        if p and Path(p).exists():
            return p, kind
    raise SystemExit("no Chrome/Edge found; set WEBADMIN_CHROME to the exe path")


def load_state() -> dict:
    if STATE.exists():
        return json.loads(STATE.read_text(encoding="utf-8"))
    raise SystemExit("no session; run `webadmin.py start` first")


def attach(state: dict | None = None) -> webdriver.Chrome:
    """Attach to the already-running browser via debuggerAddress."""
    state = state or load_state()
    opts = Options()
    opts.debugger_address = f"127.0.0.1:{state['port']}"
    if state.get("kind") == "edge":
        from selenium.webdriver.edge.options import Options as EdgeOptions
        from selenium.webdriver.edge.service import Service as EdgeService
        eo = EdgeOptions()
        eo.debugger_address = f"127.0.0.1:{state['port']}"
        return webdriver.Edge(options=eo)
    return webdriver.Chrome(options=opts)


def detach(driver) -> None:
    """Stop only our chromedriver; leave the browser running."""
    try:
        driver.service.stop()
    except Exception:
        pass


def cmd_start(args) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    if STATE.exists():
        st = json.loads(STATE.read_text(encoding="utf-8"))
        if _alive(st.get("pid")):
            print(f"already running (pid {st['pid']}, port {st['port']})")
            if args.camera or args.host:
                cam = _cam_from_args(args)
                d = attach(st)
                d.get(f"http://{cam['host']}:{cam['port']}/")
                detach(d)
                print(f"navigated to http://{cam['host']}:{cam['port']}/")
            return
    exe, kind = find_browser()
    port = args.port or DEFAULT_PORT
    profile = OUT / "chrome-profile"
    profile.mkdir(parents=True, exist_ok=True)
    cam = _cam_from_args(args) if (args.camera or args.host) else None
    start_url = f"http://{cam['host']}:{cam['port']}/" if cam else "about:blank"
    cmdline = [
        exe,
        f"--remote-debugging-port={port}",
        f"--user-data-dir={profile}",
        "--window-size=1600,1200",
        "--no-first-run",
        "--no-default-browser-check",
        "--ignore-certificate-errors",
        "--disable-features=ChromeWhatsNewUI",
        start_url,
    ]
    DETACHED = 0x00000008 | 0x00000200  # DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP
    proc = subprocess.Popen(cmdline, creationflags=DETACHED, close_fds=True)
    STATE.write_text(json.dumps({"pid": proc.pid, "port": port, "kind": kind,
                                 "exe": exe, "camera": cam}, indent=2),
                     encoding="utf-8")
    # Wait for the debugging port to come up.
    for _ in range(40):
        try:
            d = attach()
            print(f"started {kind} pid {proc.pid} on port {port}; url={d.current_url}")
            detach(d)
            if cam:
                print(f"camera: {cam['name']} {cam['manufacturer']} {cam['model']} "
                      f"@ {cam['host']}:{cam['port']} (onvif user '{cam['user']}')")
            return
        except Exception:
            time.sleep(0.25)
    raise SystemExit("browser did not expose debugging port in time")


def _alive(pid) -> bool:
    if not pid:
        return False
    out = subprocess.run(["tasklist", "/FI", f"PID eq {pid}", "/NH"],
                         capture_output=True, text=True).stdout
    return str(pid) in out


def cmd_stop(args) -> None:
    if not STATE.exists():
        print("no session")
        return
    st = json.loads(STATE.read_text(encoding="utf-8"))
    subprocess.run(["taskkill", "/PID", str(st["pid"]), "/T", "/F"],
                   capture_output=True, text=True)
    STATE.unlink(missing_ok=True)
    print(f"stopped pid {st['pid']}")


def cmd_status(args) -> None:
    if not STATE.exists():
        print("no session")
        return
    st = json.loads(STATE.read_text(encoding="utf-8"))
    alive = _alive(st.get("pid"))
    print(f"pid={st['pid']} port={st['port']} kind={st['kind']} alive={alive}")
    if alive:
        d = attach(st)
        print(f"url={d.current_url}\ntitle={d.title}")
        detach(d)


# --------------------------------------------------------------------------- #
# Per-command actions (attach -> act -> detach).
# --------------------------------------------------------------------------- #
def _cam_from_args(args) -> dict:
    if args.host:
        return {"host": args.host, "port": args.port_cam or 80,
                "user": args.user or os.environ.get("CAM_USER", "admin"),
                "password": args.passwd or os.environ.get("CAM_PASS")
                or os.environ.get("ONVIF_PASSWORD", ""),
                "name": args.host, "manufacturer": "", "model": ""}
    cam = resolve_camera(args.camera)
    if args.user:
        cam["user"] = args.user
    if args.passwd:
        cam["password"] = args.passwd
    return cam


def cmd_goto(args) -> None:
    d = attach()
    url = args.url
    if not url.startswith(("http://", "https://")):
        st = load_state()
        cam = st.get("camera") or {}
        base = f"http://{cam.get('host')}:{cam.get('port', 80)}" if cam else ""
        url = base + ("" if url.startswith("/") else "/") + url
    d.get(url)
    print(f"url={d.current_url}\ntitle={d.title}")
    detach(d)


def cmd_info(args) -> None:
    d = attach()
    print(f"url={d.current_url}\ntitle={d.title}\nframes={len(d.find_elements(By.TAG_NAME, 'iframe'))}")
    detach(d)


def cmd_shot(args) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    d = attach()
    name = args.name or "shot"
    if not name.endswith(".png"):
        name += ".png"
    path = OUT / name
    d.save_screenshot(str(path))
    print(f"saved {path}  (url={d.current_url})")
    detach(d)


def _maybe_into_frame(d, args):
    if args.frame is not None:
        frames = d.find_elements(By.TAG_NAME, "iframe")
        idx = int(args.frame)
        if idx < len(frames):
            d.switch_to.frame(frames[idx])
        else:
            print(f"frame {idx} not found ({len(frames)} frames)", file=sys.stderr)


def cmd_dom(args) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    d = attach()
    _maybe_into_frame(d, args)
    if args.selector:
        els = d.find_elements(By.CSS_SELECTOR, args.selector)
        html = "\n\n".join(e.get_attribute("outerHTML") for e in els)
    else:
        html = d.execute_script("return document.documentElement.outerHTML")
    path = OUT / "dom.html"
    path.write_text(html, encoding="utf-8")
    print(f"saved {path} ({len(html)} B)\n--- first 1500 chars ---\n{html[:1500]}")
    detach(d)


def cmd_js(args) -> None:
    d = attach()
    _maybe_into_frame(d, args)
    result = d.execute_script(args.script)
    try:
        print(json.dumps(result, ensure_ascii=False, indent=2, default=str))
    except TypeError:
        print(repr(result))
    detach(d)


def cmd_find(args) -> None:
    """Locate elements whose text/value/placeholder contains the query; print
    a CSS-ish locator and context for each, so the agent can target them."""
    d = attach()
    _maybe_into_frame(d, args)
    script = r"""
    const q = arguments[0].toLowerCase();
    const out = [];
    const all = document.querySelectorAll('*');
    for (const el of all) {
      const own = Array.from(el.childNodes)
        .filter(n => n.nodeType === 3).map(n => n.textContent).join('').trim();
      const val = el.value || '';
      const ph = el.getAttribute && (el.getAttribute('placeholder') || el.getAttribute('title') || el.getAttribute('aria-label')) || '';
      const hay = (own + ' ' + val + ' ' + ph).toLowerCase();
      if (hay.includes(q)) {
        const tag = el.tagName.toLowerCase();
        const id = el.id ? '#' + el.id : '';
        const cls = (el.className && typeof el.className === 'string')
          ? '.' + el.className.trim().split(/\s+/).slice(0,3).join('.') : '';
        const nm = el.getAttribute && el.getAttribute('name');
        out.push({ tag, id, cls, name: nm || '', type: el.getAttribute && el.getAttribute('type') || '',
                   text: own.slice(0,60), value: String(val).slice(0,40), placeholder: ph.slice(0,40) });
      }
    }
    return out.slice(0, 40);
    """
    res = d.execute_script(script, args.query)
    if not res:
        print(f"no elements containing '{args.query}'")
    for r in res:
        sel = (r["tag"] + r["id"] + r["cls"]).strip()
        extra = []
        if r["name"]:
            extra.append(f'name={r["name"]}')
        if r["type"]:
            extra.append(f'type={r["type"]}')
        if r["value"]:
            extra.append(f'value="{r["value"]}"')
        if r["placeholder"]:
            extra.append(f'ph="{r["placeholder"]}"')
        meta = ("  [" + ", ".join(extra) + "]") if extra else ""
        txt = f'  text="{r["text"]}"' if r["text"] else ""
        print(f"{sel}{meta}{txt}")
    detach(d)


def _by(args):
    return (By.XPATH, args.selector) if args.xpath else (By.CSS_SELECTOR, args.selector)


def cmd_click(args) -> None:
    d = attach()
    _maybe_into_frame(d, args)
    el = d.find_element(*_by(args))
    d.execute_script("arguments[0].scrollIntoView({block:'center'})", el)
    el.click()
    print(f"clicked {args.selector}; url={d.current_url}")
    detach(d)


def cmd_type(args) -> None:
    d = attach()
    _maybe_into_frame(d, args)
    el = d.find_element(*_by(args))
    if args.clear:
        el.clear()
    el.send_keys(args.text)
    if args.enter:
        el.send_keys(Keys.RETURN)
    print(f"typed into {args.selector}")
    detach(d)


def cmd_frames(args) -> None:
    d = attach()
    frames = d.find_elements(By.TAG_NAME, "iframe")
    print(f"{len(frames)} iframe(s):")
    for i, f in enumerate(frames):
        print(f"  [{i}] id={f.get_attribute('id')} name={f.get_attribute('name')} "
              f"src={f.get_attribute('src')}")
    detach(d)


def cmd_login(args) -> None:
    """Best-effort login. Resolves creds, finds password+username inputs and a
    submit control, fills and submits. Verify with `shot` afterwards."""
    st = load_state()
    cam = st.get("camera") or {}
    user = args.user or cam.get("user") or os.environ.get("CAM_USER", "admin")
    pwd = (args.passwd or os.environ.get("CAM_PASS")
           or os.environ.get("ONVIF_PASSWORD") or cam.get("password", ""))
    if not pwd:
        raise SystemExit("no password; pass --pass or set CAM_PASS")
    d = attach()
    _maybe_into_frame(d, args)
    # Use real keystrokes/clicks (not JS value-setting): JS-set values bypass
    # framework change detection (e.g. AngularJS digest), so login() would read
    # stale/empty model values. Native events go through the framework.
    pw_inputs = d.find_elements(By.CSS_SELECTOR, "input[type=password]")
    if not pw_inputs:
        detach(d)
        raise SystemExit("no password field found on this page")
    pw_el = pw_inputs[0]
    u_els = d.find_elements(
        By.CSS_SELECTOR,
        "input[type=text], input[type=email], input[name*=user i], "
        "input[id*=user i], input[name*=name i]")
    if u_els:
        u_els[0].clear()
        u_els[0].send_keys(user)
    pw_el.clear()
    pw_el.send_keys(pwd)
    print(f"filled: user_field={'yes' if u_els else 'no'}, password_field=yes")
    # Click an explicit login control if present; else submit via Enter.
    btns = (d.find_elements(By.CSS_SELECTOR, "button.login-btn, button[ng-click*=login i]")
            or d.find_elements(By.CSS_SELECTOR,
                               "button[type=submit], input[type=submit], "
                               ".login-btn, button.login, #login"))
    if btns:
        d.execute_script("arguments[0].scrollIntoView({block:'center'})", btns[0])
        btns[0].click()
        print(f"clicked login control ({btns[0].tag_name}.{btns[0].get_attribute('class')})")
    else:
        pw_el.send_keys(Keys.RETURN)
        print("pressed Enter in password field")
    time.sleep(2.5)
    print(f"after: url={d.current_url} title={d.title}")
    detach(d)


def cmd_creds(args) -> None:
    """Print resolved connection info for a camera. The password is never
    echoed in clear — only whether it decrypted and its length."""
    cam = _cam_from_args(args)
    pwd = cam.get("password") or ""
    info = {k: cam[k] for k in ("name", "host", "port", "user", "manufacturer", "model")}
    info["password"] = f"<decrypted, {len(pwd)} chars>" if pwd else "<empty>"
    print(json.dumps(info, ensure_ascii=False, indent=2))


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="Drive a camera web admin via Selenium.")
    sub = p.add_subparsers(dest="cmd", required=True)

    def add_frame(sp):
        sp.add_argument("--frame", help="switch into iframe by index before acting")

    sp = sub.add_parser("start", help="launch browser (optionally at a camera)")
    sp.add_argument("--camera", help="camera name or IP from the store")
    sp.add_argument("--host", help="raw host/IP (bypass store)")
    sp.add_argument("--port", type=int, help="remote-debugging port")
    sp.add_argument("--port-cam", type=int, dest="port_cam", help="camera http port")
    sp.add_argument("--user")
    sp.add_argument("--pass", dest="passwd")
    sp.set_defaults(func=cmd_start)

    sub.add_parser("stop", help="kill browser").set_defaults(func=cmd_stop)
    sub.add_parser("status", help="session status").set_defaults(func=cmd_status)
    sub.add_parser("info", help="current url/title/frames").set_defaults(func=cmd_info)
    sub.add_parser("frames", help="list iframes").set_defaults(func=cmd_frames)

    sp = sub.add_parser("goto", help="navigate (absolute url or /path on camera)")
    sp.add_argument("url")
    sp.set_defaults(func=cmd_goto)

    sp = sub.add_parser("shot", help="screenshot -> webadmin-out/<name>.png")
    sp.add_argument("name", nargs="?")
    sp.set_defaults(func=cmd_shot)

    sp = sub.add_parser("dom", help="dump outerHTML (whole page or a selector)")
    sp.add_argument("selector", nargs="?")
    add_frame(sp)
    sp.set_defaults(func=cmd_dom)

    sp = sub.add_parser("js", help="run JS, print result")
    sp.add_argument("script")
    add_frame(sp)
    sp.set_defaults(func=cmd_js)

    sp = sub.add_parser("find", help="find elements whose text/value contains QUERY")
    sp.add_argument("query")
    add_frame(sp)
    sp.set_defaults(func=cmd_find)

    sp = sub.add_parser("click", help="click first element matching selector")
    sp.add_argument("selector")
    sp.add_argument("--xpath", action="store_true")
    add_frame(sp)
    sp.set_defaults(func=cmd_click)

    sp = sub.add_parser("type", help="type text into element")
    sp.add_argument("selector")
    sp.add_argument("text")
    sp.add_argument("--xpath", action="store_true")
    sp.add_argument("--no-clear", dest="clear", action="store_false")
    sp.add_argument("--enter", action="store_true")
    add_frame(sp)
    sp.set_defaults(func=cmd_type)

    sp = sub.add_parser("login", help="best-effort web-admin login")
    sp.add_argument("--user")
    sp.add_argument("--pass", dest="passwd")
    add_frame(sp)
    sp.set_defaults(func=cmd_login)

    sp = sub.add_parser("creds", help="print resolved credentials for a camera")
    sp.add_argument("--camera")
    sp.add_argument("--host")
    sp.add_argument("--port-cam", type=int, dest="port_cam")
    sp.add_argument("--user")
    sp.add_argument("--pass", dest="passwd")
    sp.set_defaults(func=cmd_creds)
    return p


def main() -> None:
    if os.name != "nt":
        print("warning: DPAPI cred decryption works only on Windows", file=sys.stderr)
    args = build_parser().parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
