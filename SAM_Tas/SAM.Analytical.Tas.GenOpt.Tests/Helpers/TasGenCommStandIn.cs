// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

// A stand-in for the part of EDSL's TasGenComm.dll a TasGenExecute script sees (read from the installed assembly by
// reflection, PR7b): the globals object ScriptArgs (TasFiles, Variables), TasVariable, the static
// ScriptOutput.SetValue and the TasFiles namespace. The generated scripts are compiled against it in the tests
// (TasScriptCompiler), because TasGenComm.dll is not available where CI runs. Nothing here runs a script.

using System.Collections.Generic;

namespace TasGenComm
{
    public class ScriptArgs
    {
        public TasGenComm.TasFiles.TasFileManager TasFiles;
        public Dictionary<string, TasVariable> Variables;
        public double Misc;
    }

    public class TasVariable
    {
        public string VariableName { get; set; }

        public double VariableValue { get; set; }

        public double MinValue { get; set; }

        public double MaxValue { get; set; }

        public double Step { get; set; }
    }

    public class ScriptOutput
    {
        public static ScriptOutput Instance { get; } = new ScriptOutput();

        public Dictionary<string, double> Outputs { get; set; } = new Dictionary<string, double>();

        public static void SetValue(string name, double value)
        {
            Instance.Outputs[name] = value;
        }
    }
}

namespace TasGenComm.TasFiles
{
    public enum TasExtension
    {
        T3D,
        TBD,
        TPD,
        TSD,
        TWD,
    }

    public class TasFile
    {
        public string FullPath { get; set; }

        public string FileName { get; }
    }

    public class TasFileManager
    {
        public Dictionary<string, TasFile> getFiles(TasExtension tasExtension)
        {
            return new Dictionary<string, TasFile>();
        }
    }
}
