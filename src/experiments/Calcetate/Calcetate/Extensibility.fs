namespace Calcetate

open System
open System.Collections.Concurrent
open System.IO
open System.Threading.Tasks
open CalceTypes
open System.Text


module Extensibility =
    open FSharp.Compiler.Interactive.Shell
    
    let PluginCache = 
        ConcurrentDictionary<string, PluginInfo * FsiEvaluationSession>() 
        
    let private getSession stdin stdout stderr =
        let defConfig = FsiEvaluationSession.GetDefaultConfiguration()

        let argv =
            [| "fsi.exe"
               "--noninteractive"
               "--nologo"
               "--gui-" |]

        FsiEvaluationSession.Create(defConfig, argv, stdin, stdout, stderr, true)



    let LoadPluginFromScript (name: string, filepath: string) : PluginInfo option =
        match PluginCache.TryGetValue name with 
        | (true, (plugin, _)) ->
            printfn $"Loading plugin [{plugin.name}] from cache"
            Some plugin
        | (false, _) ->
            printfn $"Plugin [{name}] not in cache, loading from file [{filepath}]"

            let Fsi = getSession (new StringReader("")) (new StringWriter()) (new StringWriter())
            let content = File.ReadAllText filepath

            match Fsi.EvalInteractionNonThrowing(content) with
            | Choice1Of2 (Some value), _ ->
                match value.ReflectionType = typeof<PluginInfo> with
                | true -> unbox value.ReflectionValue |> Some
                | false -> None
            | Choice1Of2 None, _ ->
                None
            | Choice2Of2 (ex), _ ->
                eprintfn "%O" ex
                None
            |> Option.orElseWith (fun _ ->
                let bound = Fsi.TryFindBoundValue "Plugin" |> Option.orElseWith (fun _ -> Fsi.TryFindBoundValue "plugin")
                match bound with
                | Some bound ->
                    match bound.Value.ReflectionType = typeof<PluginInfo> with
                    | true -> unbox bound.Value.ReflectionValue |> Some
                    | false -> None
                | None -> None
            )
            |> Option.map(fun value -> 
                match PluginCache.TryAdd(value.name, (value, Fsi)) with 
                | true ->
                    printfn $"Plugin [{value.name}] added to cache"
                | false ->
                    printfn $"We couldn't add plugin [{value.name}] to cache"
                value
            )
