// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
// PR7a compiler probe (TasGenExecute script, not compiled by any project).
int zero = default;
var t = (zero, 2);
ScriptOutput.SetValue("Result", zero + t.Item2);
