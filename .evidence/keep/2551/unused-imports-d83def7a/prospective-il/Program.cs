using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;
var wanted = new HashSet<string>();
foreach (var file in args) {
 using var stream=File.OpenRead(file);using var pe=new PEReader(stream);var reader=pe.GetMetadataReader();
 foreach(var h in reader.TypeDefinitions){var t=reader.GetTypeDefinition(h);if(reader.GetString(t.Name)!="TownSystem")continue;
 foreach(var m in t.GetMethods())wanted.Add(reader.GetString(reader.GetMethodDefinition(m).Name));}
}
var incoming = wanted.ToDictionary(n => n, _ => new List<string>());
var declarations = wanted.ToDictionary(n => n, _ => 0);
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public).Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => unchecked((ushort)o.Value));
int bodies = 0, strings = 0;
foreach (var file in args)
{
 using var stream = File.OpenRead(file); using var pe = new PEReader(stream); var reader = pe.GetMetadataReader();
 string TypeName(EntityHandle h) => h.Kind switch {
  HandleKind.TypeDefinition => reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)h).Name),
  HandleKind.TypeReference => reader.GetString(reader.GetTypeReference((TypeReferenceHandle)h).Name), _ => "" };
 (string Owner,string Name) Method(EntityHandle h) {
  if (h.Kind == HandleKind.MethodSpecification) return Method(reader.GetMethodSpecification((MethodSpecificationHandle)h).Method);
  if (h.Kind == HandleKind.MethodDefinition) { var m=reader.GetMethodDefinition((MethodDefinitionHandle)h); return (TypeName(m.GetDeclaringType()), reader.GetString(m.Name)); }
  if (h.Kind == HandleKind.MemberReference) { var m=reader.GetMemberReference((MemberReferenceHandle)h); return (TypeName(m.Parent), reader.GetString(m.Name)); }
  return ("", "");
 }
 foreach (var handle in reader.MethodDefinitions) {
  var method=reader.GetMethodDefinition(handle); var identity=Method(handle);
  if (identity.Owner=="TownSystem" && wanted.Contains(identity.Name)) declarations[identity.Name]++;
  if (method.RelativeVirtualAddress==0) continue;
  bodies++; var il=pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!; int pos=0;
  while(pos<il.Length) {
   ushort code=il[pos++]; if(code==0xfe) code=(ushort)(0xfe00 | il[pos++]); var op=opcodes[code]; int size;
   if(op.OperandType is OperandType.InlineMethod or OperandType.InlineTok) {
    var target=Method(MetadataTokens.EntityHandle(BitConverter.ToInt32(il,pos)));
    if(target.Owner=="TownSystem" && wanted.Contains(target.Name)) incoming[target.Name].Add(Path.GetFileName(file)+":"+identity.Owner+"."+identity.Name+":"+op.Name);
   }
   if(op.OperandType==OperandType.InlineString) { var text=reader.GetUserString(MetadataTokens.UserStringHandle(BitConverter.ToInt32(il,pos)&0x00ffffff)); if(wanted.Contains(text)) strings++; }
   size=op.OperandType switch {
    OperandType.InlineNone=>0,
    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,
    OperandType.InlineVar=>2,
    OperandType.InlineI8 or OperandType.InlineR=>8,
    OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(il,pos),
    OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR=>4,
    _=>throw new InvalidDataException("Unknown operand "+op.OperandType)
   }; pos+=size;
  }
  if(pos!=il.Length) throw new InvalidDataException("Malformed IL boundary");
 }
}
Console.WriteLine(JsonSerializer.Serialize(new {bodies,declarations,incoming,exact_reflection_name_literals=strings},new JsonSerializerOptions {WriteIndented=true}));
