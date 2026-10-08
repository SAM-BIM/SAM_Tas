// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
// PR7a compiler probe (TasGenExecute script, not compiled by any project).
static class Block
{
    public static double Twice(double x) { return 2 * x; }
}
ScriptOutput.SetValue("Result", Block.Twice(4));
