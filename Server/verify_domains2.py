import urllib.request, json

domains = [
    "https://labshieldprotocol.my.id/",
    "https://carlomuzaqi.my.id/",
    "https://makerslab.engineer/",
    "https://toolora.cloud/",
    "https://smarthydroponic.my.id/",
]
for url in domains:
    try:
        req = urllib.request.Request(url, method="GET")
        resp = urllib.request.urlopen(req, timeout=15)
        body = resp.read()[:100]
        print(f"{url:40s} {resp.status} OK (body: {body[:50]})")
    except urllib.request.HTTPError as e:
        print(f"{url:40s} {e.code} {e.reason}")
    except Exception as e:
        print(f"{url:40s} ERROR {str(e)[:50]}")
