// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

namespace ECS.World

open System.Runtime.InteropServices
open System
open System.Collections.Generic
open Foundation.Containers
open Foundation.Types
open ECS.Entity

// The World owns every entity and every component array. There is no per-entity
// "object" - an entity is just an index into these shared tables.
type World() =
    // Master table of entities. The stored BitMask is the source of truth for
    // "which components does this entity have" - inserting gives a fresh, all-zero
    // mask, and IdTable's own generation check protects against stale/reused Entity ids.
    let entityMasks = IdTable<BitMask>()

    // Assigns and remembers a stable bit index (0-63) per component type, so the
    // same type always maps to the same bit in every entity's BitMask.
    let componentTypes = ComponentTypeRegistry()

    // One dense array per component type, boxed as `obj` because a single Dictionary
    // can't hold different generic ResizeArray<'T> instantiations directly.
    // Retrieved and cast back to ResizeArray<'T> with `:?>` wherever it's used.
    let componentStores = Dictionary<Type, obj>()

    // Allocates a new entity slot with an empty (all components absent) mask.
    member this.CreateEntity() : Entity =
        entityMasks.Insert(BitMask())

    // Invalidates the entity's id (bumps its generation in entityMasks) so any
    // Entity value still referring to it is safely detected as stale afterwards.
    member this.DestroyEntity(entity: Entity) =
        entityMasks.Remove(entity)

    // Attaches (or overwrites) a component of type 'T on the given entity.
    member this.AddComponent<'T>(entity: Entity, value: 'T) =
        let componentType = typeof<'T>

        // Get this component type's backing array, creating it the first time
        // this type is ever used. TryGetValue comes back from .NET as a
        // (bool, value) tuple, matched directly with `true, x` / `false, _`.
        let store =
            match componentStores.TryGetValue(componentType)with
            |true, existing -> existing :?> ResizeArray<'T>
            |false, _ ->
                let newStore = ResizeArray<'T>()
                componentStores.[componentType] <- newStore
                newStore

        // Pad the array with default values until it's long enough to be indexed
        // directly by this entity's index (this is a plain flat array, not an
        // IdTable - the entity's own generation is already validated separately).
        while store.Count <= entity.Index do
            store.Add(Unchecked.defaultof<'T>)

        store.[entity.Index] <- value

        // Flag this component as present in the entity's mask. The mask has to be
        // fetched, mutated locally, and written back explicitly with Update,
        // because BitMask is a value type - mutating a copy wouldn't affect what's
        // actually stored inside entityMasks.
        match entityMasks.TryGet(entity) with
        | true, mask ->
            mask.SetBit(componentTypes.GetIndex<'T>())
            entityMasks.Update(entity, mask)
        | false, _ -> ()

    //It checks if we have a component on our Cog (gameobhect) and if we do we get the 'T and if we didnt we get None (null)
    member this.Has<'T>(entity: Entity) : bool =
        match entityMasks.TryGet(entity) with
        | true, mask -> mask.IsBitSet(componentTypes.GetIndex<'T>())
        | false, _ -> false

    member this.GetComponent<'T>(entity: Entity) : 'T option =
        if not (this.Has<'T>(entity)) then
            None
        else
            let store = componentStores.[typeof<'T>] :?> ResizeArray<'T>
            Some store.[entity.Index]

    // C#-friendly, allocation-free component lookup for per-frame systems.
    member this.TryGetComponent<'T>(entity: Entity, [<Out>] value: byref<'T>) : bool =
        value <- Unchecked.defaultof<'T>

        match entityMasks.TryGet(entity) with
        | true, mask when mask.IsBitSet(componentTypes.GetIndex<'T>()) ->
            let store =
                componentStores.[typeof<'T>]
                :?> ResizeArray<'T>

            value <- store.[entity.Index]
            true

        | _ ->
            false

    member this.RemoveComponent<'T>(entity: Entity) =
        if this.Has<'T>(entity) then
            match entityMasks.TryGet(entity) with
            | true, mask ->
                mask.ClearBit(componentTypes.GetIndex<'T>())
                entityMasks.Update(entity, mask)
            | false, _ -> ()

            let store = componentStores.[typeof<'T>] :?> ResizeArray<'T>
            store.[entity.Index] <- Unchecked.defaultof<'T>
    
    // Finds the lowest-index live entity containing both component types.
    // The loop creates no iterator, option value, or temporary collection.
    member this.TryFindFirst<'T1, 'T2>(
        predicate: Func<'T1, 'T2, bool>,
        [<Out>] entity: byref<Entity>) : bool =

        entity <- Id.Invalid

        match componentStores.TryGetValue(typeof<'T1>),
              componentStores.TryGetValue(typeof<'T2>) with

        | (true, firstStoreObject),
          (true, secondStoreObject) ->

            let firstStore =
                firstStoreObject :?> ResizeArray<'T1>

            let secondStore =
                secondStoreObject :?> ResizeArray<'T2>

            // These are resolved once, outside the entity loop.
            let firstBit = componentTypes.GetIndex<'T1>()
            let secondBit = componentTypes.GetIndex<'T2>()

            let mutable index = 0
            let mutable found = false

            while not found &&
                  index < entityMasks.SlotCount do

                match entityMasks.TryGetSlot(index) with
                | true, candidate, mask ->
                    if mask.IsBitSet(firstBit) &&
                       mask.IsBitSet(secondBit) &&
                       predicate.Invoke(
                           firstStore.[index],
                           secondStore.[index]) then

                        entity <- candidate
                        found <- true

                | false, _, _ ->
                    ()

                index <- index + 1

            found

        | _ ->
            false
