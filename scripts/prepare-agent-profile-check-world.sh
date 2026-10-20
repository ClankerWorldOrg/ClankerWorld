#!/usr/bin/env bash
# Produce disposable native owner data for the fresh-process Godot profile check.
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="$(realpath -m "${1:?Supply an output JSON path}")"
probe_dir="$(mktemp -d)"
trap 'rm -rf -- "${probe_dir}"' EXIT

# Reuse the hard-world and accepted-birth fixtures, including their long IDs,
# Unicode names and real household/food/parentage operations.
python3 - "${repo_root}" "${probe_dir}" <<'PY'
import pathlib
import sys
import xml.etree.ElementTree as ET

root, probe = map(pathlib.Path, sys.argv[1:])
tests = ET.parse(root / "tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj")
xunit = tests.find(".//PackageReference[@Include='xunit']").attrib["Version"]
project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
properties = ET.SubElement(project, "PropertyGroup")
for name, value in {"OutputType": "Exe", "TargetFramework": "net10.0",
                    "ImplicitUsings": "enable", "Nullable": "enable"}.items():
    ET.SubElement(properties, name).text = value
items = ET.SubElement(project, "ItemGroup")
ET.SubElement(items, "ProjectReference", Include=str(root / "src/ClankerWorld.Server/ClankerWorld.Server.csproj"))
ET.SubElement(items, "PackageReference", Include="xunit", Version=xunit)
ET.SubElement(items, "Using", Include="Xunit")
for name in ["NormalPathWorld", "SecondGenerationFixture", "ChosenBirthNameTestFixture"]:
    ET.SubElement(items, "Compile", Include=str(root / f"tests/ClankerWorld.Tests/TestSupport/{name}.cs"))
ET.SubElement(items, "Compile", Include=str(root / "tests/ClankerWorld.Tests/TestSupport/ModelWork/ManualModelWorkScheduler.cs"))
ET.SubElement(items, "Compile", Include=str(root / "scripts/prepare-agent-profile-check-world.cs"))
ET.ElementTree(project).write(probe / "ProfileCheckWorld.csproj", encoding="utf-8")
PY
dotnet restore "${repo_root}/src/ClankerWorld.Server/ClankerWorld.Server.csproj" --locked-mode
timeout 600 dotnet run --project "${probe_dir}/ProfileCheckWorld.csproj" --configuration Release -- "${output}"
