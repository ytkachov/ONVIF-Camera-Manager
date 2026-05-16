"""Walk Hikvision ISAPI endpoints to enumerate every exposed parameter
(the web admin is just a UI on top of these). Save XML responses to files."""
import os
import re
import urllib.request as ur
from urllib.error import HTTPError, URLError
from pathlib import Path

IP = os.environ.get("CAM_IP", "192.168.1.58")
PORT = int(os.environ.get("CAM_PORT", "80"))
USER = os.environ.get("CAM_USER", "admin")
PASS = os.environ.get("CAM_PASS", "H586.020d")
OUT = Path(os.environ.get("OUT_DIR", "d:/Projects/AI/Claude/ONVIF/tools/probe-out/isapi"))
OUT.mkdir(parents=True, exist_ok=True)

_pwd_mgr = ur.HTTPPasswordMgrWithDefaultRealm()
_pwd_mgr.add_password(None, f"http://{IP}:{PORT}/", USER, PASS)
_opener = ur.build_opener(ur.HTTPDigestAuthHandler(_pwd_mgr),
                          ur.HTTPBasicAuthHandler(_pwd_mgr))


def get(path: str, out_name: str | None = None) -> str | None:
    url = f"http://{IP}:{PORT}{path}"
    try:
        with _opener.open(url, timeout=15) as r:
            body = r.read().decode("utf-8", errors="replace")
            status = r.status
    except HTTPError as e:
        body = (e.read() or b"").decode("utf-8", errors="replace")
        status = e.code
    except URLError as e:
        body = f"<error>{e}</error>"
        status = -1
    if out_name is None:
        out_name = re.sub(r'[^A-Za-z0-9._-]+', '_', path.strip("/")) + ".xml"
    (OUT / out_name).write_text(body, encoding="utf-8")
    short = body[:80].replace("\n", " ").encode("ascii", "replace").decode("ascii")
    print(f"  [{status:3}] {path}  -> {out_name}  ({len(body)} B)  {short}")
    return body if status == 200 else None


def walk_capabilities():
    """Hit /ISAPI/System/capabilities and follow links into per-module
    capability endpoints (this is how Hikvision advertises features)."""
    print("=== root capabilities ===")
    get("/ISAPI/System/capabilities", "00-capabilities.xml")


CORE = [
    "/ISAPI/System/deviceInfo",
    "/ISAPI/System/Network/interfaces",
    "/ISAPI/System/Network/interfaces/1/ipAddress",
    "/ISAPI/System/Network/interfaces/1/wireless",
    "/ISAPI/System/Network/Discovery/UPnP",
    "/ISAPI/System/Network/extern",
    "/ISAPI/System/Network/EZVIZ",
    "/ISAPI/System/Network/EZVIZ/capabilities",
    "/ISAPI/System/Network/SNMP",
    "/ISAPI/System/Network/DDNS",
    "/ISAPI/System/Network/PPPoE",
    "/ISAPI/System/Network/QoS",
    "/ISAPI/System/Network/Bonjour",
    "/ISAPI/System/Network/SSH",
    "/ISAPI/System/Network/telnet",
    "/ISAPI/Security/users",
    "/ISAPI/Security/AAA/userPermission/1",
    "/ISAPI/Security/illegalLoginLock",
    "/ISAPI/Security/onlineUser",
    "/ISAPI/System/time",
    "/ISAPI/System/time/ntpServers/1",
    "/ISAPI/System/time/localTime",
    "/ISAPI/System/Holidays",
    "/ISAPI/System/upgradeStatus",
    "/ISAPI/System/diagnosedData/healthDetect",
    "/ISAPI/System/IO/inputs",
    "/ISAPI/System/IO/outputs",
    "/ISAPI/Streaming/channels",
    "/ISAPI/Streaming/channels/101",
    "/ISAPI/Streaming/channels/102",
    "/ISAPI/Streaming/channels/101/capabilities",
    "/ISAPI/Streaming/channels/101/picture",
    "/ISAPI/ContentMgmt/InputProxy/channels",
    "/ISAPI/ContentMgmt/Storage",
    "/ISAPI/ContentMgmt/record/tracks",
    "/ISAPI/Image/channels/1",
    "/ISAPI/Image/channels/1/capabilities",
    "/ISAPI/Image/channels/1/colorEx",
    "/ISAPI/Image/channels/1/sharpness",
    "/ISAPI/Image/channels/1/exposure",
    "/ISAPI/Image/channels/1/whiteBalance",
    "/ISAPI/Image/channels/1/dayNightFilter",
    "/ISAPI/Image/channels/1/WDR",
    "/ISAPI/Image/channels/1/BLC",
    "/ISAPI/Image/channels/1/HLC",
    "/ISAPI/Image/channels/1/noiseReduce",
    "/ISAPI/Image/channels/1/imageEnhancement",
    "/ISAPI/Image/channels/1/ISPMode",
    "/ISAPI/Image/channels/1/IrcutFilter",
    "/ISAPI/Image/channels/1/IRLight",
    "/ISAPI/Image/channels/1/shutter",
    "/ISAPI/Image/channels/1/powerLineFrequency",
    "/ISAPI/Image/channels/1/gain",
    "/ISAPI/Image/channels/1/lensInitialization",
    "/ISAPI/Image/channels/1/lensCorrection",
    "/ISAPI/Image/channels/1/EIS",
    "/ISAPI/System/Video/inputs/channels/1/overlays",
    "/ISAPI/Smart/MotionDetection/1",
    "/ISAPI/Smart/MotionDetectionExt/1",
    "/ISAPI/Smart/LineDetection/1",
    "/ISAPI/Smart/FieldDetection/1",
    "/ISAPI/Smart/regionEntrance/1",
    "/ISAPI/Smart/regionExiting/1",
    "/ISAPI/Smart/unattendedBaggage/1",
    "/ISAPI/Smart/attendedBaggage/1",
    "/ISAPI/Smart/loitering/1",
    "/ISAPI/Smart/peopleDetection/1",
    "/ISAPI/Smart/faceContrastInfoSearch/capabilities",
    "/ISAPI/Smart/channels/1",
    "/ISAPI/Event/triggers",
    "/ISAPI/Event/notification/httpHosts",
    "/ISAPI/Event/notification/alertStream",
    "/ISAPI/Event/notification/EventTriggers",
    "/ISAPI/PTZCtrl/channels/1",
    "/ISAPI/PTZCtrl/channels/1/capabilities",
]

def main():
    walk_capabilities()
    print("=== probing endpoints ===")
    for p in CORE:
        get(p)

if __name__ == "__main__":
    main()
