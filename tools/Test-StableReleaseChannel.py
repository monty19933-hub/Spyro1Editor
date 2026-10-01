#!/usr/bin/env python3
"""Source-free package identity selection. Does not build or publish packages."""
import os, subprocess, unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
class StableChannel(unittest.TestCase):
    def select(self,**env):
        prefix='set -e; PUBLIC_RELEASE_VERSION=6; PROJECT_VERSION=0.1.0-beta.54; BETA_RELEASE_NUMBER=6; PUBLIC_RELEASE_NAME="Spyro Editor Beta V6"; PUBLIC_RELEASE_SLUG=SpyroEditor-Beta-V6; RELEASE_MANIFEST_SCHEMA_VERSION=1; '
        command=prefix+'source "$1"; printf "%s\\n" "$PUBLIC_RELEASE_NAME" "$PUBLIC_RELEASE_SLUG" "$PROJECT_VERSION" "$RELEASE_MANIFEST_SCHEMA_VERSION" "$RELEASE_IDENTITY_VERSION" "${RELEASE_PUBLISH_PROPERTIES[@]}"'
        return subprocess.run(['bash','-c',command,'test',str(ROOT/'tools/ReleaseChannel.sh')],env={**os.environ,'SPYRO_EDITOR_RELEASE_CHANNEL':'beta',**env},capture_output=True,text=True)
    def test_legacy_unchanged(self):
        r=self.select();self.assertEqual(r.returncode,0,r.stderr);self.assertEqual(r.stdout.splitlines()[:5],['Spyro Editor Beta V6','SpyroEditor-Beta-V6','0.1.0-beta.54','1','6'])
    def test_stable_optin(self):
        r=self.select(SPYRO_EDITOR_RELEASE_CHANNEL='stable',SPYRO_EDITOR_STABLE_VERSION='1.0',SPYRO_EDITOR_STABLE_INTERNAL_VERSION='1.0.0');self.assertEqual(r.returncode,0,r.stderr);self.assertEqual(r.stdout.splitlines()[:5],['Spyro Editor 1.0','SpyroEditor-V1.0','1.0.0','3','stable:1.0']);self.assertIn('-p:ReleaseChannel=stable',r.stdout)
    def test_source_default_and_explicit_beta(self):
        project=ROOT/'src/Spyro.Editor.App/Spyro.Editor.App.csproj'
        command='set -e; PUBLIC_RELEASE_VERSION=6; BETA_RELEASE_NUMBER=6; source "$1" "$2"; source "$3"; printf "%s\\n" "$RELEASE_CHANNEL" "$PUBLIC_RELEASE_NAME" "$PROJECT_VERSION" "${RELEASE_PUBLISH_PROPERTIES[@]}"'
        clean={k:v for k,v in os.environ.items() if not k.startswith('SPYRO_EDITOR_')}
        args=['bash','-c',command,'test',str(ROOT/'tools/Read-ReleaseVersion.sh'),str(project),str(ROOT/'tools/ReleaseChannel.sh')]
        r=subprocess.run(args,env=clean,capture_output=True,text=True);self.assertEqual(r.returncode,0,r.stderr);self.assertEqual(r.stdout.splitlines()[:3],['stable','Spyro Editor 1.0.1','1.0.1'])
        r=subprocess.run(args,env={**clean,'SPYRO_EDITOR_RELEASE_CHANNEL':'beta'},capture_output=True,text=True);self.assertEqual(r.returncode,0,r.stderr);self.assertEqual(r.stdout.splitlines()[0],'beta');self.assertIn('0.1.0-beta.54',r.stdout);self.assertIn('-p:ReleaseChannel=beta',r.stdout)
    def test_missing_identity(self):self.assertNotEqual(self.select(SPYRO_EDITOR_RELEASE_CHANNEL='stable').returncode,0)
    def test_stable_changelog_previous_release(self):
        project=ROOT/'src/Spyro.Editor.App/Spyro.Editor.App.csproj'
        command='set -e; PUBLIC_RELEASE_VERSION=6; BETA_RELEASE_NUMBER=6; source "$1" "$2"; source "$3"; printf "%s\\n" "$PUBLIC_RELEASE_NAME" "$PREVIOUS_PUBLIC_RELEASE_NAME"'
        clean={k:v for k,v in os.environ.items() if not k.startswith('SPYRO_EDITOR_')}
        r=subprocess.run(['bash','-c',command,'test',str(ROOT/'tools/Read-ReleaseVersion.sh'),str(project),str(ROOT/'tools/ReleaseChannel.sh')],env=clean,capture_output=True,text=True)
        self.assertEqual(r.returncode,0,r.stderr)
        current,previous=r.stdout.splitlines()
        self.assertEqual(previous,'Spyro Editor 1.0')
        lines=(ROOT/'CHANGELOG.md').read_text().splitlines()
        self.assertEqual(lines[:3],['# '+current,'','## Changes since '+previous])
        self.assertEqual(sum(line.startswith('# Spyro Editor ') for line in lines),1)
    def test_previous_stable_must_be_older(self):
        for previous in ('1.0.1','1.1','01.0','garbage'):
            with self.subTest(previous=previous):
                r=self.select(SPYRO_EDITOR_RELEASE_CHANNEL='stable',SPYRO_EDITOR_STABLE_VERSION='1.0.1',SPYRO_EDITOR_STABLE_INTERNAL_VERSION='1.0.1',SOURCE_PREVIOUS_STABLE_VERSION=previous)
                self.assertNotEqual(r.returncode,0)
    def test_invalid_channels_and_versions(self):
        for channel,public,internal in [('release','1.0','1.0.0'),('stable','1','1.0.0'),('stable','1.0.0','1.0.0'),('stable','01.0','1.0.0'),('stable','1.0','0.1.0-beta.54'),('stable','1.0','1.1.0')]:
            with self.subTest(channel=channel,public=public,internal=internal):self.assertNotEqual(self.select(SPYRO_EDITOR_RELEASE_CHANNEL=channel,SPYRO_EDITOR_STABLE_VERSION=public,SPYRO_EDITOR_STABLE_INTERNAL_VERSION=internal).returncode,0)
if __name__=='__main__':unittest.main()
