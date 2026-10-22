from pathlib import Path
p=Path('tests/ClankerWorld.Tests/Simulation/World/ToolProgressionRuntimeTests.cs');s=p.read_text()
a=s.index('    [Fact]\n    public async Task MultiMaterialToolRepairDoesNotCollectAnInputWhenTheCompleteLoadCannotFit()')
b=s.index('    [Fact]\n    public async Task MultiMaterialToolRepairCollectsBothInputsAndCompletesAcrossReload()',a)
c=s.index('    [Fact]\n    public async Task WoodSourceSkipsNearerTreesWithoutAnAxeAndKeepsTheNearestChoiceAcrossReload()',b)
blocked=s[a:b];fit=s[b:c]
setup=blocked[blocked.index('        using var setup'):blocked.index('        Assert.Equal(1, PersonalEquipmentRules.FreeCapacity')]
setup=setup.replace('"tool-repair-multi-capacity-blocked",','hasRoom\n                ? "tool-repair-multi-capacity-fit" : "tool-repair-multi-capacity-blocked",')
setup=setup.replace('        inventory = InventoryAuthority.AddLot(inventory, "reserved-repair-ballast", "fiber", actor, 6);','        if (!hasRoom)\n            inventory = InventoryAuthority.AddLot(inventory, "reserved-repair-ballast", "fiber", actor, 6);')
setup=setup.replace('        inventory = InventoryAuthority.Reserve(inventory, "held-repair-ballast", actor,\n            "reserved-repair-ballast", 6, "pending_trade", state.Society.Society.WorldTick + 1_000);','        if (!hasRoom)\n            inventory = InventoryAuthority.Reserve(inventory, "held-repair-ballast", actor,\n                "reserved-repair-ballast", 6, "pending_trade", state.Society.Society.WorldTick + 1_000);')
chooser=blocked[blocked.index('        var candidateId ='):blocked.index('        for (var tick = 0; tick < 4; tick++)')]
blockedbody=blocked[blocked.index('        for (var tick = 0; tick < 4; tick++)'):blocked.rindex('    }')].rstrip()
fitbody=fit[fit.index('        for (var tick = 0; tick < 20'):fit.rindex('    }')].rstrip()
def indent(body): return '\n'.join('    '+line if line else '' for line in body.splitlines())
new='''    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultiMaterialToolRepairRequiresRoomForBothInputsAndCompletesAcrossReload(bool hasRoom)
    {
'''+setup+'''        if (!hasRoom)
            Assert.Equal(1, PersonalEquipmentRules.FreeCapacity(inventory, actor, null));

'''+chooser+'''        if (!hasRoom)
        {
'''+indent(blockedbody)+'''
        }
        else
        {
'''+indent(fitbody)+'''
        }
    }

'''
s=s[:a]+new+s[c:]
a=s.index('    [Theory]\n    [InlineData("wooden_axe", "empty")]');b=s.index('    [Theory]\n    [InlineData("tree")]',a)
warehouse=s[a:b]
warehouse=warehouse.replace('[InlineData("wooden_axe", ', '[InlineData(')
warehouse=warehouse.replace('    public async Task SharedWorkToolsRespectTownWarehouseAccessAndReservationsAcrossReload(\n        string itemKind, string scenario)\n    {','    public async Task SharedWorkToolsRespectTownWarehouseAccessAndReservationsAcrossReload(string scenario)\n    {\n        const string itemKind = "wooden_axe";')
warehouse=warehouse.replace('        if (scenario != "own")\n','')
warehouse=warehouse.replace('        var candidateId = itemKind == "wooden_axe" ? "collect_wooden_axe" : "collect_wooden_hoe";','        const string candidateId = "collect_wooden_axe";')
warehouse=warehouse.replace('if (scenario is "own" or "empty")','if (scenario == "empty")')
s=s[:a]+warehouse+s[b:]
p.write_text(s)
