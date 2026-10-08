// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
// PR7a compiler probe (TasGenExecute script, not compiled by any project).
int n = 0;
n += typeof(TBD.TBDDocument) != null ? 1 : 0;
n += typeof(TSD.TSDDocument) != null ? 1 : 0;
n += typeof(TPD.TPDDoc) != null ? 1 : 0;
n += typeof(TAS3D.T3DDocument) != null ? 1 : 0;
n += typeof(TWD.Document) != null ? 1 : 0;
ScriptOutput.SetValue("Result", n);
