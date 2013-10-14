﻿// BEGIN PREAMBLE -- do not evaluate, for intellisense only
#r "Nessos.MBrace.Utils.dll"
#r "Nessos.MBrace.Actors.dll"
#r "Nessos.MBrace.Base.dll"
#r "Nessos.MBrace.Store.dll"
#r "Nessos.MBrace.Client.dll"

open Nessos.MBrace.Client

// END OF PREAMBLE

// Calculates pi using the Monte Carlo method

#load "Utils.fs"

open System
open System.Numerics

// the classic monte carlo implementation
// take random points and see of they are inside the circle or not
let monteCarloPiWorker (iterations : bigint) =
//    let rng = new System.Random()
    let rng = new RandomNumberGenerator (bufsize = 256)

    // the actual test for a single point
    let inline checkNextSample () =
        let r = int64 Int32.MaxValue
        let x = int64 (rng.Next())
        let y = int64 (rng.Next())
        x * x + y * y <= r * r

    // iterates through native ints for performance
    let smallMonteCarloPiWorker (iterations : int) =
        let mutable acc = 0
        for i = 1 to iterations do
            if checkNextSample () then acc <- acc + 1
        acc

    let mutable acc = 0I
    let mutable remainingSamples = iterations

    while remainingSamples / bigint Int32.MaxValue > 0I do
        acc <- acc + bigint (smallMonteCarloPiWorker Int32.MaxValue)
        remainingSamples <- remainingSamples - bigint Int32.MaxValue

    acc <- acc + bigint (smallMonteCarloPiWorker (int remainingSamples))

    acc

let rec getDigits dividend divisor = seq {
    let div, rem = BigInteger.DivRem(dividend,divisor)
    yield div
    yield! getDigits (rem * 10I) divisor
}

[<Cloud>]
let calculatePi (iterations : bigint) (digits : int) : ICloud<string> =
    cloud {
        let! workers = Cloud.GetWorkerCount()
        let workers = bigint (2 * workers)
        let iterationsPerWorker = iterations / workers
        let rem = iterations % workers

        let runWorker iterations = cloud { return monteCarloPiWorker iterations }
        
        let! results = 
            [| 
                for i in 1I .. workers -> runWorker iterationsPerWorker

                if rem > 0I then yield runWorker rem
            |]
            |> Cloud.Parallel

        let sum = results |> Array.sum

        let a = 4I * sum 
        let b = iterations

        return getDigits a b
               |> Seq.take digits
               |> Seq.map string
               |> String.concat ""
    }

// test

let runtime = MBrace.InitLocal 6

// run for 1000000000 iterations and get 10 digits
runtime.Run <@ calculatePi 1000000000I 10 @>