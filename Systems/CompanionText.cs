using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Terraria.Localization;

namespace PalicoCompanion.Systems;

// UI text resolves at draw time. Deferred messages carry native NetworkText
// through the existing string status/receipt fields, so each client uses its
// own language without changing the packet layout or persisted identifiers.
internal static class CompanionText {
    const string Root="Mods.PalicoCompanion.Text.";
    static readonly Regex Embedded=new("\u001e([A-Za-z0-9+/=]+)\u001f",RegexOptions.Compiled);
    static readonly Dictionary<string,string> constants=new();
    internal static string Get(string key,params object[] args)=>Language.GetTextValue(Root+key,args);
    internal static string Message(string key,params object[] args)=>MessageFull(Root+key,args);
    internal static string MessageFull(string key,params object[] args){
        if(args.Length==0&&constants.TryGetValue(key,out string cached))return cached;
        var substitutions=args.Select(a=>a is string s&&s.StartsWith('\u001e')&&s.EndsWith('\u001f')?(object)Decode(s[1..^1]):a).ToArray();
        using var stream=new MemoryStream();
        using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))NetworkText.FromKey(key,substitutions).Serialize(writer);
        string message="\u001e"+Convert.ToBase64String(stream.ToArray())+"\u001f";
        if(args.Length==0)constants[key]=message;
        return message;
    }
    static NetworkText Decode(string data){using var stream=new MemoryStream(Convert.FromBase64String(data));using var reader=new BinaryReader(stream);return NetworkText.Deserialize(reader);}
    internal static string Resolve(string text)=>Embedded.Replace(text??"",match=>{
        try{return Decode(match.Groups[1].Value).ToString();}
        catch(Exception e) when(e is FormatException or IOException or ArgumentException){return "";}
    });
}
