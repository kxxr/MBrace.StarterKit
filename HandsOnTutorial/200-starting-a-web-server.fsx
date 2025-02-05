﻿(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

open System
open System.IO
open MBrace.Core
open MBrace.Flow

(**
# Starting a Web Server in your Cluster

This tutorial illustrates creating a cloud process which 
acts as a web server to monitor and control the cluster.
*)

#load "lib/webserver.fsx"
#load "lib/sieve.fsx"

open Suave
open Suave.Types
open Suave.Http
open Suave.Http.Applicatives
open Suave.Http.Successful
open Suave.Web
open Webserver

(**
This Suave request is executed in response to a GET on 

   http://nn.nn.nn.nn/cluster/workers

It uses cluster.GetWorkers to access information from the cluster. 
*)

(** This is the Suave specification for the web server we will run in the cluster: *)
let helloRequest ctxt = 
    async { return! OK "hello" ctxt }

let getCluster() = 
    Config.GetCluster() 

(**
This Suave request is executed in response to a GET on 

   http://nn.nn.nn.nn/

It uses cluster.GetWorkers to access information from the cluster. 
*)
let getWorkersRequest ctxt = 
    async {
            let cluster = getCluster()
            let workers = cluster.Workers
            let msg = 
              [ yield "<html>"
                yield Angular.header
                yield "<body>"
                yield! workers |> Angular.table ["ID";"Heartbeat"] (fun w -> 
                    [ w.Id; sprintf "%A" w.LastHeartbeat ])
                yield "</body>"
                yield "</html>" ]
              |> String.concat "\n"
            let! rsp = OK msg ctxt 
            return rsp}

(**
This Suave request is executed in response to a GET on 
   http://nn.nn.nn.nn/cluster/logs

It uses cluster.GetLogs to access information from the cluster. 
*)
let getLogsRequest ctxt = 
    async {
//            let logs = cluster.GetSystemLogs()
            let msg = 
              [ yield "<html>"
                yield Angular.header
                yield "<body>"
//                yield! logs |> Angular.table ["Time";"Message"] (fun w -> 
//                    [ sprintf "%A" w.Time; w.Message ])
                yield "</body>"
                yield "</html>" ]
              |> String.concat "\n"
            return! OK msg ctxt  }

// This Suave request is executed in response to a GET on 
//   http://nn.nn.nn.nn/cluster/submit/primes/%d
//
// It uses cluster.Submit to create a new job in the cluster.
let computePrimesRequest n ctxt = 
    async {
            let cluster = getCluster()
            let task = 
              cluster.Submit 
                (cloud { let primes = Sieve.getPrimes n
                         return sprintf "calculated %d primes: %A" primes.Length primes })
            let msg = 
              [ yield "<html>"
                yield Angular.header
                yield "<body>"
                yield (sprintf "<p>Created job %s</p>" task.Id)
                yield "</body>"
                yield "</html>" ]
              |> String.concat "\n"
            return! OK msg ctxt  }

// This Suave request is executed in response to a GET on 
//   http://nn.nn.nn.nn/cluster/job/%d
//
// It uses cluster.GetProcess to create a new job in the cluster.
let getJobRequest v ctxt = 
    async {
            let cluster = getCluster()
            let task = cluster.GetProcessById v
            let msg = 
                [ yield "<html>"
                  yield Angular.header
                  yield "<body>"
                  yield (sprintf "<p>Job %s, Completed: %A, Result: %s</p>" task.Id task.Status (try if task.Status = MBrace.Runtime.CloudProcessStatus.Completed then sprintf "%A" (task.AwaitResultBoxed()) else "" with _ -> "<err>") )
                  yield "</body>"
                  yield "</html>" ]
                |> String.concat "\n"
            return! OK msg ctxt  }

(** Now the overall specification of the server: *)
let webServerSpec () =
  choose
    [ GET >>= choose
                [ path "/" >>= OK "Welcome to the cluster" 
                  path "/cluster/workers" >>= getWorkersRequest
                  path "/cluster/logs" >>= getLogsRequest
                  pathScan "/cluster/job/%s" getJobRequest
                  pathScan "/cluster/submit/primes/%d" computePrimesRequest ]
      POST >>= choose
                [ path "/hello" >>= OK "Hello POST"
                  path "/goodbye" >>= OK "Good bye POST" ] ]

(** 
Now connect to the cluster: 
*)

let cluster = getCluster()

cluster.ShowProcesses()

// Use this to inspect the endpoints we can bind to in the cluster
let endPointNames = 
    cloud { return Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment.CurrentRoleInstance.InstanceEndpoints.Keys |> Seq.toArray }
    |> cluster.Run

(**
By default, MBrace.Azure clusters created using Brisk engine on Azure allow us to bind to 

* HTTP endpoint 'DefaultHttpEndpoint'
* HTTP endpoint 'MBraceStats'
* TCP endpoint 'DefaultTcpEndpoint'

Here we bind to DefaultHttpEndpoint.  
*)
let serverJob = suaveServerInCloud "DefaultHttpEndpoint" webServerSpec |> cluster.Submit

(**
After you start the webserver (by binding to this internal endpoint), the website will 
be published to the corresponding public endpoint. You can find the IP
address or URL for the public endpoint by looking for 'Input Endpoints'
in the 'Configuration' section of the Azure management console page for the 
Cloud Service for the MBrace cluster.

For example, the public URL for the MBraceWorkerRole may be http://191.233.103.54:80
*)


(** Use this to inspect the status of the web server job: *) 
serverJob.ShowInfo()

(** If the webserver doesn't start, then use this to see the logging output from webserver.fsx: *) 

serverJob.ShowLogs()

(** Use this to cancel the web server (via the distributed cancellationToken being cancelled): *)
// serverJob.Cancel()

(** Use this to wait for the web server to exit after cancellation: *)
// serverJob.Resut