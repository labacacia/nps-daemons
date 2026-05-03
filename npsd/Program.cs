// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0
//
// npsd — NPS Daemon, Layer 1 (host-local NCP wire + state host).
// See docs/daemons/architecture.md for the role this binary plays in
// the broader NPS deployment topology.

using NPS.Daemon.Npsd;

NpsdHost.Build(args).Run();

// Test bridge: NPS.Tests references npsd's classes via InternalsVisibleTo
// (see Npsd.csproj). The factory for tests is `NpsdHost.BuildForTests`.
