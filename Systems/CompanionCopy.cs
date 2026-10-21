using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Terraria.Localization;
namespace PalicoCompanion.Systems;

// Translate the old internal status protocol at its presentation boundary.
// Work state, network payloads and success-prefix checks remain unchanged.
internal static class CompanionCopy {
    internal static IEnumerable<string> Wrap(string text,int width){
        // Vanilla's space-based wrapper treats an entire Chinese clause as one
        // word, leaving short lines after embedded keys/numbers. Measure with
        // the same native font, allowing CJK character boundaries instead.
        bool cjk=false;foreach(char c in text)if(c>='\u2e80'&&c<='\u9fff'){cjk=true;break;}
        if(cjk){foreach(var row in WrapCjk(text,width))yield return row;yield break;}
        var rows=Terraria.Utils.WordwrapString(text,Terraria.GameContent.FontAssets.MouseText.Value,width,100,out int last);
        for(int i=0;i<=last;i++){
            string row=(rows[i]??"").TrimEnd();
            // Vanilla wraps CJK runs as long words and inserts Latin hyphens.
            // Keep its word/number layout, but omit that artificial CJK suffix.
            if(row.Length>=2&&row[^1]=='-'&&row[^2]>='\u2e80'&&row[^2]<='\u9fff')row=row[..^1];
            yield return row;
        }
    }
    static IEnumerable<string> WrapCjk(string text,int width){
        var font=Terraria.GameContent.FontAssets.MouseText.Value;
        bool Word(char c)=>char.IsAsciiLetterOrDigit(c)||c is '.' or '%';
        foreach(string paragraph in text.Split('\n')){
            string rest=paragraph.Trim();if(rest.Length==0){yield return "";continue;}
            while(rest.Length>0){
                if(font.MeasureString(rest).X<=width){yield return rest;break;}
                int low=1,high=rest.Length;
                while(low<high){int mid=(low+high+1)/2;if(font.MeasureString(rest[..mid]).X<=width)low=mid;else high=mid-1;}
                int end=low;
                if(end<rest.Length&&Word(rest[end-1])&&Word(rest[end])){
                    int word=end;while(word>0&&Word(rest[word-1]))word--;
                    if(word>0)end=word;
                }
                while(end>1&&("，。；：？！、）】》”’!?;:)]%".Contains(rest[end])||"（【《“‘(".Contains(rest[end-1])))end--;
                yield return rest[..end].TrimEnd();rest=rest[end..].TrimStart();
            }
        }
    }
    static readonly Dictionary<string,string> Text=new(){
        ["停止：主人不可用"]="施工已停止：等待玩家返回或复活",
        ["停止：背包不可用"]="无法读取呆猫背包，请稍后重试",
        ["停止：猫的位置无效"]="无法确定呆猫位置，请召回后重试",
        ["等待：猫需要走近施工点"]="正在靠近施工点",
        ["停止：猫背包缺少未收藏的材料"]="呆猫背包材料不足（收藏物品不消耗）",
        ["失败：原版物件放置判定未通过，未消耗材料"]="此处无法放置，材料未消耗",
        ["No construction order"]="暂无施工任务",
        ["Cancel the active construction order first"]="请先取消当前施工任务",
        ["Construction requires a living player in single player"]="手动施工仅限单人，请在复活后使用",
        ["Invalid cursor position"]="无法确定起点，请重新选择",
        ["Length must be 1 to 24 tiles"]="施工长度应为 1～24 格",
        ["Platforms require horizontal direction -1 or +1"]="平台请选择向左或向右",
        ["Rope orders build downward only"]="绳索请选择向下",
        ["Only wood platforms and rope support construction orders"]="手动施工支持木平台和绳索",
        ["Preview a construction order first"]="请先选择起点并预览施工",
        ["Construction confirmed; walking to first tile"]="已确认，正在前往施工起点",
        ["Construction stopped: player or world changed"]="角色或世界已改变，施工已停止",
        ["Construction paused"]="施工已暂停",
        ["Construction waiting for danger to pass"]="附近有敌人，稍后继续施工",
        ["Construction stopped: owner is more than 30 tiles away"]="离开施工点超过 30 格，施工已停止",
        ["Construction stopped: tile unreachable for 10 seconds"]="暂时无法到达施工点，请先打通通道",
        ["Construction stopped: invalid cat position"]="无法确定呆猫位置，请召回后重试",
        ["Construction requires the original living player in single player"]="请在单人模式复活后重新下达任务",
        ["Preview belongs to another world"]="世界已改变，请重新选择起点",
        ["Construction start must be within 12 tiles of the player"]="请选择你周围 12 格内的起点",
        ["Preview touches the world boundary"]="施工路线靠近世界边界，请更换起点",
        ["Preview touches world spawn protection"]="自动施工会避开世界出生点，请更换起点",
        ["Preview touches bed spawn protection"]="自动施工会避开床的出生点，请更换起点",
        ["Preview touches housing walls"]="自动施工会避开房屋背景墙，请更换起点",
        ["Preview touches chest protection"]="自动施工会避开箱子附近，请更换起点",
        ["Preview touches town NPC protection"]="自动施工会避开城镇居民附近，请更换起点",
        ["Cancel the current mining request first"]="请先取消当前采矿任务",
        ["Mining assistance requires a living single player"]="请在复活后重新选择采矿目标",
        ["Invalid mining target"]="无法确定矿块位置，请重新选择",
        ["Mining target touches the world boundary"]="矿块靠近世界边界，无法开采",
        ["Mining target is obstructed"]="通往矿块的路径被挡住了",
        ["Mining cancelled by pause"]="已暂停，采矿任务取消",
        ["Mining stopped: no pickaxe in your backpack"]="没有可用的镐，采矿已停止",
        ["Mining stopped after 10 seconds; check reach or pickaxe power"]="暂时挖不到这块矿，请检查通路和镐力",
        ["Ore already cleared; remaining work finished"]="目标矿块已挖除，任务结束",
        ["Invalid cat position"]="无法确定呆猫位置，请召回后重试",
        ["Mining stopped: target changed"]="目标方块已改变，采矿停止",
        ["Mining stopped: owner unavailable or multiplayer"]="当前无法继续协助，采矿停止",
        ["Mining stopped: world changed"]="世界已改变，采矿停止",
        ["Mining stopped: working tool changed"]="可用工具已改变，采矿停止",
        ["Mining target outside safe world bounds"]="矿块靠近世界边界，无法开采",
        ["Mining stopped: selected ore changed"]="目标矿块已改变，采矿停止",
        ["Walk with cat to authorized chest; unpause"]="请让呆猫靠近已获准使用且未被打开的箱子",
        ["Unfavorite the item first"]="请先取消物品的收藏标记",
        ["Open a normal chest to authorize it"]="请先打开一个普通箱子，再授予呆猫使用许可",
        ["Summon the cat first"]="请先召出呆猫",
        ["Paused"]="已暂停",
        ["Following"]="跟随主人",
        ["Resting"]="休息中",
        ["Guarding"]="警戒中",
    };
    static readonly (Regex pattern,string replacement)[] Patterns=[
        (new Regex("^Mining requested: (\\d+) ore tiles; stay nearby$"),"已选择 $1 块矿，请留在附近"),
        (new Regex("^Mining cancelled; (\\d+)/(\\d+) mined, drops retained$"),"采矿已取消 · 已完成 $1/$2，掉落物保留"),
        (new Regex("^Mining complete: (\\d+)/(\\d+); normal drops retained$"),"采矿完成 · $1/$2"),
        (new Regex("^Mining (\\d+)/(\\d+); approaching next ore$"),"已开采 $1/$2，正在前往下一块"),
        (new Regex(" \\((\\d+)/(\\d+) mined; retained\\)$")," · 已开采 $1/$2"),
        (new Regex(" \\((\\d+)/(\\d+) placed; retained\\)$")," · 已建造 $1/$2"),
        (new Regex("^Stored (\\d+) items matching chest contents$"),"已存入 $1 件同类物品"),
        (new Regex("^Stored (\\d+) items$"),"已存入 $1 件物品"),
        (new Regex("^Construction cancelled; (\\d+) placed tiles retained$"),"施工已取消，保留已建造的 $1 格"),
        (new Regex("^Walking to construction tile (\\d+)/(\\d+)$"),"前往施工点 $1/$2"),
        (new Regex("^Working on tile (\\d+)/(\\d+)$"),"正在施工 $1/$2"),
        (new Regex("^Construction complete: (\\d+) tiles$"),"施工完成 · 共 $1 格"),
        (new Regex("^Placed (\\d+)/(\\d+); walking to next tile$"),"已建造 $1/$2，前往下一格"),
        (new Regex("^Insufficient un-favorited materials: (\\d+)/(\\d+)$"),"材料不足 · 可用 $1 / 需要 $2（不消耗收藏物品）"),
        (new Regex("^Preview blocked at \\((\\d+),(\\d+)\\); existing tiles are never replaced$"),"路线中已有方块（$1，$2），请选择其他起点"),
        (new Regex("^Preview unsafe near \\((\\d+),(\\d+)\\): liquid or wiring$"),"路线靠近液体或电线（$1，$2），请选择其他起点"),
        (new Regex("^Preview: (\\d+) rope at .*; budget \\d+ items. Confirm to start.$"),"向下铺设 $1 格绳索，需 $1 根绳索；确认后开始"),
        (new Regex("^Preview: (\\d+) platform at .*direction \\(-1,0\\); budget \\d+ items. Confirm to start.$"),"向左铺设 $1 格平台，需 $1 块木平台；确认后开始"),
        (new Regex("^Preview: (\\d+) platform at .*direction \\(1,0\\); budget \\d+ items. Confirm to start.$"),"向右铺设 $1 格平台，需 $1 块木平台；确认后开始"),
    ];
    internal static string Status(string text){
        text=CompanionText.Resolve(text);
        if(text.StartsWith(SafeConstruction.SuccessPrefix,StringComparison.Ordinal))text=text[SafeConstruction.SuccessPrefix.Length..];
        if(string.IsNullOrEmpty(text)||!Language.ActiveCulture.Name.StartsWith("zh",StringComparison.OrdinalIgnoreCase))return text??"";
        foreach(var (pattern,replacement) in Patterns)text=pattern.Replace(text,replacement);
        foreach(var entry in Text)if(text.StartsWith(entry.Key,StringComparison.Ordinal))return entry.Value+text[entry.Key.Length..];
        return text;
    }
}
