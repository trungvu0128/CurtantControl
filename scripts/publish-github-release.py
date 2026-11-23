#!/usr/bin/env python3
"""Upload a verified full device bundle to GitHub Releases using local Git credentials."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import urllib.error
import urllib.parse
import urllib.request

REPO = 'trungvu0128/CurtantControl'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('version')
    parser.add_argument('--small-from', action='append', default=[], help='Attach the verified small update from this installed version')
    parser.add_argument('--notes-file', type=Path, help='Prepend release-specific Markdown notes')
    args = parser.parse_args()
    if not re.fullmatch(r'\d+\.\d+\.\d+', args.version):
        raise ValueError('Use MAJOR.MINOR.PATCH.')
    notes = args.notes_file.read_text(encoding='utf-8-sig').strip() if args.notes_file else ''
    root = Path(__file__).resolve().parents[1]
    if subprocess.check_output(['git','status','--porcelain'],cwd=str(root)).strip():
        raise ValueError('Commit source changes before publishing a release.')
    commit = subprocess.check_output(['git','rev-parse','HEAD'],cwd=str(root),universal_newlines=True).strip()
    archive = root / ('artifacts/bundles/CurtantControl-Final-v%s-pi-buster-arm.tar.gz' % args.version)
    checksum = Path(str(archive)+'.sha256')
    if not archive.is_file() or not checksum.is_file():
        raise ValueError('Build the complete device bundle first.')
    expected = checksum.read_text().split()
    if expected != [hashlib.sha256(archive.read_bytes()).hexdigest(),archive.name]:
        raise ValueError('Local bundle checksum mismatch.')
    files = [archive, checksum]
    for baseline in args.small_from:
        if not re.fullmatch(r'\d+\.\d+\.\d+', baseline):
            raise ValueError('Small update baseline must use MAJOR.MINOR.PATCH.')
        small = root / ('artifacts/updates/CurtantControl-Update-v%s-to-v%s-pi-buster-arm.zip' % (baseline, args.version))
        small_checksum = Path(str(small) + '.sha256')
        if small_checksum.read_text().split() != [hashlib.sha256(small.read_bytes()).hexdigest(), small.name]:
            raise ValueError('Small update checksum mismatch.')
        files.extend((small, small_checksum))
    credential_env = dict(os.environ, GIT_TERMINAL_PROMPT='0', GCM_INTERACTIVE='Never')
    credential = subprocess.run(['git','credential','fill'],input='protocol=https\nhost=github.com\n\n',
                                stdout=subprocess.PIPE,stderr=subprocess.PIPE,universal_newlines=True,env=credential_env,check=True)
    values = dict(line.split('=',1) for line in credential.stdout.splitlines() if '=' in line)
    token = values.get('password')
    if not token:
        raise ValueError('GitHub authentication unavailable. Sign in with Git Credential Manager first.')

    def api(path, method='GET', data=None, upload=False):
        host = 'uploads.github.com' if upload else 'api.github.com'
        headers = {'Authorization':'Bearer '+token,'User-Agent':'CurtantControl-release-publisher',
                   'Accept':'application/vnd.github+json'}
        if isinstance(data,dict):
            data=json.dumps(data).encode(); headers['Content-Type']='application/json'
        elif data is not None:
            headers['Content-Type']='application/octet-stream'
        request=urllib.request.Request('https://'+host+path,data=data,headers=headers,method=method)
        try:
            with urllib.request.urlopen(request,timeout=240) as response:
                return json.load(response)
        except urllib.error.HTTPError as error:
            # Do not print request headers or credential-helper output.
            raise ValueError('GitHub request failed: HTTP %d (%s)' % (error.code,path)) from None

    releases=api('/repos/'+REPO+'/releases')
    tag='v'+args.version
    existing=next((item for item in releases if item['tag_name']==tag),None)
    if existing is not None and (not existing['draft'] or existing['target_commitish'] != commit):
        raise ValueError('Release already exists for another commit or is published; choose a new version.')
    body=('Complete Pi4 Raspbian Buster ARM32 app + keycheck installer.\n\n'
          'Automatic update check on startup, Developer Updates, verified download, backup and restart.\n'
          'Install once with sh install.sh. Existing settings/PIN/state preserved.\n'
          'Legacy .NET8 build; cross-published and tested on host, not verified on a physical Pi.\n\n'
          'Source commit: '+commit)
    if notes:
        body = notes + '\n\n' + body
    release=existing or api('/repos/'+REPO+'/releases','POST',{'tag_name':tag,'target_commitish':commit,'name':'CurtantControl '+args.version,
                                               'body':body,'draft':True,'prerelease':False})
    for file in files:
        print('Uploading '+file.name,flush=True)
        content=file.read_bytes()
        previous=next((asset for asset in release.get('assets',[]) if asset['name']==file.name),None)
        if previous is not None:
            if previous.get('digest') != 'sha256:'+hashlib.sha256(content).hexdigest():
                raise ValueError('Draft asset differs. Remove it from the draft before retrying.')
            continue
        asset=api('/repos/'+REPO+'/releases/%d/assets?name=%s' % (release['id'],urllib.parse.quote(file.name)),
                  'POST',content,True)
        if asset.get('size') != len(content):
            raise ValueError('Uploaded asset size mismatch; release remains draft.')
        digest=asset.get('digest')
        if digest and digest != 'sha256:'+hashlib.sha256(content).hexdigest():
            raise ValueError('Uploaded asset checksum mismatch; release remains draft.')
    published=api('/repos/'+REPO+'/releases/'+str(release['id']),'PATCH',{'draft':False,'make_latest':'true'})
    print('Published: '+published['html_url'],flush=True)


if __name__=='__main__':
    try:
        main()
    except (ValueError,OSError,subprocess.SubprocessError) as error:
        raise SystemExit('RELEASE FAILED: '+str(error))
