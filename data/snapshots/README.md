# Snapshots

Saved copies of official pages whose sites block automated fetchers (UCD's CloudFront returns 403).
`scripts/ingest.py` tries the live URL first and uses the file here only if that fails.
Citations always point to the official URL, not the file.

To add or refresh one: open the URL in a normal browser, **Save Page As → "Webpage, HTML Only"**
using the filename given in `../sources.txt`, then re-run ingest. Re-save when ingest lists the page
under "ingested from saved snapshot" and the official page may have changed.
