﻿
#I "bin/Debug/"

#r "MBrace.Core.dll"
#r "FsPickler.dll"
#r "Vagabond.dll"
#r "MBrace.Azure.Runtime.Common.dll"
#r "MBrace.Azure.Runtime.dll"
#r "MBrace.Azure.Client.dll"

open MBrace
open MBrace.Azure.Runtime
open MBrace.Azure.Client

let config = 
    { Configuration.Default with
        StorageConnectionString = ""
        ServiceBusConnectionString = "" }


let runtime = Runtime.GetHandle(config)
runtime.AttachLogger(new Common.ConsoleLogger()) 


runtime.ShowWorkers()
runtime.ShowProcesses()

let helloJob = runtime.CreateProcess(cloud { return "Hello world"})
helloJob.AwaitResult()


let parallelJob =
    [1..10]
    |> List.map (fun i -> cloud { return i * i})
    |> Cloud.Parallel
    |> runtime.CreateProcess

parallelJob.ShowInfo()

parallelJob.AwaitResult()



let ps = 
    Cloud.Parallel(cloud { return System.Environment.MachineName })
    |> runtime.CreateProcess

ps.AwaitResult()