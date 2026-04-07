using System;
using System.IO;
using MoonSharp.Interpreter;
using ScriptQuest.Entities;

namespace ScriptQuest.Scripting;

public class LuaEngine
{
    public LuaEngine()
    {
        Script.DefaultOptions.DebugPrint = s => Console.WriteLine($"[Lua] {s}");
    }

    /// <summary>
    /// Execute an entity's Lua script for one tick.
    /// </summary>
    public void ExecuteScript(Entity entity, EntityManager entityManager)
    {
        if (string.IsNullOrEmpty(entity.ScriptPath) || !File.Exists(entity.ScriptPath))
            return;

        if (entity.IsStunned)
            return;

        try
        {
            var script = new Script(CoreModules.Preset_SoftSandbox);

            // Create the API wrapper and self table
            var api = new LuaAPI(entity, entityManager);
            var selfTable = api.CreateSelfTable(script);

            // Load and execute the script
            string luaCode = File.ReadAllText(entity.ScriptPath);
            script.DoString(luaCode);

            // Call the on_tick function
            var onTick = script.Globals.Get("on_tick");
            if (onTick.Type == DataType.Function)
            {
                script.Call(onTick, selfTable);
            }
        }
        catch (ScriptRuntimeException ex)
        {
            Console.WriteLine($"[Lua Error] {entity.Name}: {ex.DecoratedMessage}");
        }
        catch (InternalErrorException ex)
        {
            Console.WriteLine($"[Lua Limit] {entity.Name}: Script exceeded instruction limit. {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Script Error] {entity.Name}: {ex.Message}");
        }
    }
}
