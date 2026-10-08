// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
// PR7a compiler probe (TasGenExecute script, not compiled by any project).
int Twice(int x) => 2 * x;
(int a, double b) pair = (1, 2.5);
bool ok = int.TryParse("21", out var parsed);
object boxed = parsed;
if (boxed is int i && ok) { ScriptOutput.SetValue("Result", Twice(i) + pair.a + pair.b); }
