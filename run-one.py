import pathlib,subprocess,sys,os,json,hashlib
p=pathlib.Path(__file__).parent;checkout=pathlib.Path(sys.argv[1]);side=sys.argv[2];selection=(p/'filter.txt').read_text().strip();project='tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj'
common=['dotnet','test',project,'-c','Release','--no-build','--filter',selection]
listed=subprocess.run(common+['--list-tests'],cwd=checkout,text=True,capture_output=True,check=True).stdout
(p/(side+'-selected.txt')).write_text(listed);cases=[s.strip() for s in listed.splitlines() if s.startswith('    ')];methods={s.split('(')[0] for s in cases};assert len(cases)==112,(side,len(cases));assert len(methods)==48,(side,len(methods))
expected={x['test'] for x in json.loads((p/'consumer-methods.json').read_text())};assert methods==expected,(methods-expected,expected-methods)
for path,h in json.loads((p/'production-hashes.json').read_text()).items():
 f=checkout/'tests/ClankerWorld.Tests/bin/Release/net10.0'/pathlib.Path(path).name;assert hashlib.sha256(f.read_bytes()).hexdigest()==h,str(f)
env=dict(os.environ,DOTNET_PROCESSOR_COUNT='4',ClankerWorld__Clock__StrictTickFailures='true')
print(side,'selected112cases48methods',flush=True)
subprocess.run(common+['--settings',str(p/'coverage-identities.runsettings'),'--collect','XPlat Code Coverage','--results-directory',str(p/side),'--logger','trx;LogFileName=tests.trx','--','xUnit.MaxParallelThreads=4'],cwd=checkout,env=env,check=True)
