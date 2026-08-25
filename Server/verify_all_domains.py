import urllib.request, ssl, json

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

domains = [
    "https://labshieldprotocol.my.id/",
    "https://carlomuzaqi.my.id/",
    "https://makerslab.engineer/",
    "https://toolora.cloud/",
    "https://smarthydroponic.my.id/",
]

for url in domains:
    try:
        req = urllib.request.Request(url, method="HEAD")
        resp = urllib.request.urlopen(req, timeout=15, context=ctx)
        print(f"{url:40s} {resp.status} OK")
    except urllib.request.HTTPError as e:
        print(f"{url:40s} {e.code} {e.reason}")
    except Exception as e:
        print(f"{url:40s} ERROR {str(e)[:40]}")
