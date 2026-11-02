import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

spec=importlib.util.spec_from_file_location('release_gate',Path(__file__).resolve().parents[1]/'tools/check_release.py')
g=importlib.util.module_from_spec(spec);spec.loader.exec_module(g)

class ReleaseGateTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        self.root=Path(self.tmp.name)
        for d in ['data','docs','build/android']: (self.root/d).mkdir(parents=True)
        self.write('data/monetization.json',{'revenuecat_android_key':'goog_testfixture','rewarded_android':'ca-app-pub-12345/67890'})
        (self.root/'project.godot').write_text('config/version="1.1.0"\ngeneral/android/app_id="ca-app-pub-12345~67890"\n')
        (self.root/'export_presets.cfg').write_text('package/unique_name="com.coddykit.borderlineempire"\ngradle_build/target_sdk="36"\n')
        self.write('google-services.json',{'client':[{'client_info':{'android_client_info':{'package_name':'com.coddykit.borderlineempire'}}}]})
        self.bundle=self.root/'build/android/borderlineempire.aab';self.bundle.write_bytes(b'fixture only')
        self.sha=hashlib.sha256(self.bundle.read_bytes()).hexdigest()
        self.native={'package':'com.coddykit.borderlineempire','device':'fixture','tested_at':'2026-10-13','artifact_sha256':self.sha,'version_code':1791900000,'checks':{n:{'status':'passed','evidence':'fixture log'} for n in g.CHECKS}}
        self.write('build/android/borderlineempire.release.json',{'package':self.native['package'],'sha256':self.sha,'version_code':1791900000,'version_name':'1.1.0'})
        self.write('docs/analytics-store-declarations.json',{'android':{'package':self.native['package'],'analytics_declared':True,'evidence':'fixture console readback'}})
        self.write('docs/native-sandbox-verification.json',{'android':self.native})
    def write(self,name,value): (self.root/name).write_text(json.dumps(value))
    def check(self): return g.gate(self.root,['android'])
    def test_android_does_not_require_ios(self): self.assertEqual(self.check(),[])
    def test_empty_native_file_fails(self):
        self.write('docs/native-sandbox-verification.json',{});self.assertTrue(self.check())
    def test_failed_purchase_fails(self):
        self.native['checks']['purchase']['status']='failed';self.write('docs/native-sandbox-verification.json',{'android':self.native});self.assertTrue(any('purchase evidence' in x for x in self.check()))
    def test_stale_binary_fails(self):
        self.bundle.write_bytes(b'changed binary');self.assertTrue(any('hash/version' in x for x in self.check()))
    def test_old_package_fails(self):
        self.native['package']='com.borderlineempire.game';self.write('docs/native-sandbox-verification.json',{'android':self.native});self.assertTrue(self.check())
    def test_malformed_checks_fail_without_crash(self):
        self.native['checks']=[];self.write('docs/native-sandbox-verification.json',{'android':self.native});self.assertTrue(self.check())
    def test_config_only_is_explicit(self):
        self.write('docs/native-sandbox-verification.json',{});self.assertEqual(g.gate(self.root,['android'],True),[])
    def test_old_draft_cannot_pass(self):
        self.write('build/android/borderlineempire.release.json',{'package':self.native['package'],'sha256':self.sha,'version_code':1791611984,'version_name':'1.1.0'});self.assertTrue(any('newer' in x for x in self.check()))

if __name__=='__main__': unittest.main()
