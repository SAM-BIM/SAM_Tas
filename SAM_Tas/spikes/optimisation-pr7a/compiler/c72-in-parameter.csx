// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
// PR7a compiler probe (TasGenExecute script, not compiled by any project).
double Sum(in double a, in double b) => a + b;
ScriptOutput.SetValue("Result", Sum(1, 2));
