"""Independent ONVIF/ISAPI probe for verification against the app's reads."""
import base64
import hashlib
import os
import sys
import time
import datetime as dt
import urllib.request as ur
from urllib.error import HTTPError, URLError
from pathlib import Path

IP = os.environ.get("CAM_IP", "192.168.1.58")
PORT = int(os.environ.get("CAM_PORT", "80"))
USER = os.environ.get("CAM_USER", "onvif_admin")
PASS = os.environ.get("CAM_PASS", "H586.020d")
OUT = Path(os.environ.get("OUT_DIR", "/tmp/camera-probe"))
OUT.mkdir(parents=True, exist_ok=True)


def ws_security(user: str, pwd: str) -> str:
    nonce = os.urandom(24)
    created = dt.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.000Z")
    digest = base64.b64encode(
        hashlib.sha1(nonce + created.encode() + pwd.encode()).digest()
    ).decode()
    return (
        '<wsse:Security soap:mustUnderstand="true">'
        '<wsse:UsernameToken>'
        f'<wsse:Username>{user}</wsse:Username>'
        f'<wsse:Password Type="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest">{digest}</wsse:Password>'
        f'<wsse:Nonce EncodingType="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary">{base64.b64encode(nonce).decode()}</wsse:Nonce>'
        f'<wsu:Created>{created}</wsu:Created>'
        '</wsse:UsernameToken>'
        '</wsse:Security>'
    )


NS = (
    'xmlns:soap="http://www.w3.org/2003/05/soap-envelope" '
    'xmlns:wsse="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd" '
    'xmlns:wsu="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd" '
    'xmlns:tt="http://www.onvif.org/ver10/schema" '
    'xmlns:tds="http://www.onvif.org/ver10/device/wsdl" '
    'xmlns:trt="http://www.onvif.org/ver10/media/wsdl" '
    'xmlns:tptz="http://www.onvif.org/ver20/ptz/wsdl"'
)


def soap_envelope(body: str) -> bytes:
    sec = ws_security(USER, PASS)
    return (
        f'<soap:Envelope {NS}>'
        f'<soap:Header>{sec}</soap:Header>'
        f'<soap:Body>{body}</soap:Body>'
        '</soap:Envelope>'
    ).encode()


_pwd_mgr = ur.HTTPPasswordMgrWithDefaultRealm()
_pwd_mgr.add_password(None, f"http://{IP}:{PORT}/", USER, PASS)
_opener = ur.build_opener(ur.HTTPDigestAuthHandler(_pwd_mgr),
                          ur.HTTPBasicAuthHandler(_pwd_mgr))


def soap_call(svc: str, action: str, body: str, out_file: str) -> None:
    url = f"http://{IP}:{PORT}/onvif/{svc}"
    data = soap_envelope(body)
    req = ur.Request(
        url,
        data=data,
        headers={
            "Content-Type": f'application/soap+xml; charset=utf-8; action="{action}"',
            "SOAPAction": f'"{action}"',
        },
        method="POST",
    )
    try:
        with _opener.open(req, timeout=20) as r:
            content = r.read()
    except HTTPError as e:
        content = e.read() if e.fp else f"HTTP {e.code} {e.reason}".encode()
    except URLError as e:
        content = f"URL error: {e.reason}".encode()
    (OUT / out_file).write_bytes(content)
    print(f"  -> {out_file} ({len(content)} bytes)")
    time.sleep(0.3)


CALLS = [
    ("device_service", "http://www.onvif.org/ver10/device/wsdl/GetDeviceInformation",
     "<tds:GetDeviceInformation/>", "01-devinfo.xml"),
    ("device_service", "http://www.onvif.org/ver10/device/wsdl/GetScopes",
     "<tds:GetScopes/>", "02-scopes.xml"),
    ("device_service", "http://www.onvif.org/ver10/device/wsdl/GetHostname",
     "<tds:GetHostname/>", "03-hostname.xml"),
    ("device_service", "http://www.onvif.org/ver10/device/wsdl/GetServices",
     "<tds:GetServices><tds:IncludeCapability>true</tds:IncludeCapability></tds:GetServices>",
     "04-services.xml"),
    ("device_service", "http://www.onvif.org/ver10/device/wsdl/GetSystemDateAndTime",
     "<tds:GetSystemDateAndTime/>", "05-time.xml"),
    ("device_service", "http://www.onvif.org/ver10/device/wsdl/GetCapabilities",
     "<tds:GetCapabilities><tds:Category>All</tds:Category></tds:GetCapabilities>", "06-caps.xml"),
    ("device_service", "http://www.onvif.org/ver10/device/wsdl/GetNetworkInterfaces",
     "<tds:GetNetworkInterfaces/>", "07-netif.xml"),
    ("device_service", "http://www.onvif.org/ver10/device/wsdl/GetDNS",
     "<tds:GetDNS/>", "08-dns.xml"),
    ("device_service", "http://www.onvif.org/ver10/device/wsdl/GetNTP",
     "<tds:GetNTP/>", "09-ntp.xml"),
    ("device_service", "http://www.onvif.org/ver10/device/wsdl/GetNetworkDefaultGateway",
     "<tds:GetNetworkDefaultGateway/>", "10-gateway.xml"),
    ("device_service", "http://www.onvif.org/ver10/device/wsdl/GetUsers",
     "<tds:GetUsers/>", "11-users.xml"),
    ("media_service", "http://www.onvif.org/ver10/media/wsdl/GetProfiles",
     "<trt:GetProfiles/>", "20-profiles.xml"),
    ("media_service", "http://www.onvif.org/ver10/media/wsdl/GetVideoEncoderConfigurations",
     "<trt:GetVideoEncoderConfigurations/>", "21-encoders.xml"),
    ("media_service", "http://www.onvif.org/ver10/media/wsdl/GetVideoSources",
     "<trt:GetVideoSources/>", "22-videosources.xml"),
    ("media_service", "http://www.onvif.org/ver10/media/wsdl/GetVideoSourceConfigurations",
     "<trt:GetVideoSourceConfigurations/>", "23-vscfg.xml"),
    ("media_service", "http://www.onvif.org/ver10/media/wsdl/GetAudioSources",
     "<trt:GetAudioSources/>", "24-audiosources.xml"),
]


def main():
    print(f"Probing {IP}:{PORT} as {USER}")
    for svc, action, body, out in CALLS:
        print(f"-> {action.split('/')[-1]} via {svc}")
        soap_call(svc, action, body, out)


if __name__ == "__main__":
    main()


def fetch_stream_uris():
    import re
    p = (OUT / "20-profiles.xml").read_text()
    tokens = re.findall(r'Profiles[^>]*token="([^"]+)"', p)
    for tok in tokens:
        body = (
            '<trt:GetStreamUri>'
            '<trt:StreamSetup>'
            '<tt:Stream>RTP-Unicast</tt:Stream>'
            '<tt:Transport><tt:Protocol>RTSP</tt:Protocol></tt:Transport>'
            '</trt:StreamSetup>'
            f'<trt:ProfileToken>{tok}</trt:ProfileToken>'
            '</trt:GetStreamUri>'
        )
        out = f"25-streamuri-{tok}.xml"
        print(f"-> GetStreamUri {tok}")
        soap_call("media_service", "http://www.onvif.org/ver10/media/wsdl/GetStreamUri", body, out)

if __name__ == "__main__":
    fetch_stream_uris()
