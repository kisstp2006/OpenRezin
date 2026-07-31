// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

module ECS.Entity

// An Entity carries no data of its own - it's just a lightweight alias for the
// Foundation Id type (index + generation), used as a key into the World's tables.
type Entity = Foundation.Types.Id

