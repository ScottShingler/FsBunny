﻿namespace FsBunny

open System
open System.Linq
open System.Collections.Generic
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols

/// ReliableResult contains message and the session-bound unique id used for acknowlegements.
type ReliableResult<'T> = 
    { msg : 'T
      id : uint64 }

/// Queue discriminated union identifier.
type Queue = 
    | Persistent of string
    | Temporary
    with member x.AutoAck = (match x with | Temporary -> true | _ -> false)

/// Exchange discriminated union identifier.
type Exchange = 
    | Routed of name : string * topic : string
    | Direct of string

/// EventPublisher abstraction for publishing events to an exchange.
type Publisher<'T> = 'T -> unit

/// Consumer interface.
type Consumer<'T> = 
    abstract member Get : int<s> -> ReliableResult<'T> option
    abstract member Ack : uint64 -> unit
    abstract member Nack : uint64 -> unit

/// Assembles a message from RMQ primitives (topic, headers, payload)
type Assembler<'T> = (string * IDictionary<string, obj> * byte []) -> 'T

/// Disassembles a message into RMQ primitives
type Disassembler<'T> = 'T->(Exchange*IDictionary<string, obj> option * byte [])

/// EventStreams is a factory for constructing consumers and publishers.
type EventStreams = 
    
    /// Default exchange.
    abstract Default : unit -> Exchange
    
    /// Default routed exchange using the specified routing key.
    abstract Routed : string -> Exchange
    
    /// Construct a consumer, using specified message type, queue, the exchange to bind to and the assember.
    abstract GetConsumer<'T> : Queue -> Exchange -> Assembler<'T> -> Consumer<'T>
    
    /// Construct a publisher for the specified message type and disassembler.
    abstract GetPublisher<'T> : Disassembler<'T> -> Publisher<'T>
    
    /// Use a publisher with a continuation for the specified message type and disassembler.
    abstract UsingPublisher<'T> : Disassembler<'T> -> (Publisher<'T> -> unit) -> unit

/// Consumer extensions.
[<RequireQualifiedAccessAttribute>]
module Consumer = 
    
    /// Convert auto-acking consumer into ISeq<_>
    /// selector: result mapper.
    /// consumer: event stream consumer to use
    let toSeq selector (consumer : Consumer<'T>) = 
        seq {
            while true do
                yield selector <| consumer.Get
        }

/// Infix operators.
[<AutoOpenAttribute>]
module Operators = 
    let inline (|->) (msg:^M) (withPublisher:(Publisher<'M> -> unit) -> unit) : unit=
        withPublisher (fun send -> send msg)
        