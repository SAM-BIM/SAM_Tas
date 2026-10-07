// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Math;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// A GenOpt-compatible optimisation definition (parameters, objectives, algorithm, settings and the TasGenExecute
    /// script) for a Tas workspace. It is run by <see cref="RunNative"/>; the GenOpt-format file objects
    /// (<see cref="CommandFile"/>, <see cref="ConfigFile"/>, ...) describe the same definition in GenOpt's syntax and
    /// are kept for compatibility. The legacy Java GenOpt execution route (<c>Run()</c>: GenOpt.bat, java, genopt.jar,
    /// <c>cmd /c</c>, the Tas Manager project registry) was retired in PR6.
    /// </summary>
    public class GenOptDocument
    {
        private string directory = null;

        private Script script = new Script(string.Empty);
        private Files files = Create.Files();
        private List<IParameter> parameters = new List<IParameter>();
        private Algorithm algorithm = new GoldenSectionAlgorithm();
        private OptimizationSettings optimizationSettings = new OptimizationSettings();
        private ObjectiveFunctionLocation objectiveFunctionLocation = new ObjectiveFunctionLocation();

        public GenOptDocument(string directory)
        {
            this.directory = directory;
        }

        private string GetDirectory()
        {
            if (string.IsNullOrWhiteSpace(directory) || !System.IO.Directory.Exists(directory))
            {
                return null;
            }

            return directory;
        }

        /// <summary>
        /// Runs this optimisation natively: the SAM.Math kernel drives <c>TasGenExecute.exe</c> directly
        /// (TASGENEXECUTE_PROTOCOL.md). Java, GenOpt, cmd.exe and the Tas Manager registry are not used. This is the
        /// only way to run a <see cref="GenOptDocument"/>.
        /// <para>
        /// Supported algorithms are GPSHookeJeeves and GoldenSection (<see cref="Convert.NativeAlgorithmTypes"/>), with
        /// GenOpt-compatible settings. Every other algorithm, GPSCoordinateSearch included, throws
        /// <see cref="System.NotSupportedException"/>. Invalid settings, or settings GenOpt itself would not accept, throw
        /// <see cref="GenOptCompatibilityException"/> before anything runs.
        /// </para>
        /// <para>
        /// The run gets its own folder under <paramref name="runsDirectory"/> (default: <c>SAM_NativeGenOpt</c> in the
        /// workspace). It holds the project snapshot (Script.txt plus the workspace's T3D/TBD/TPD/TSD/TWD files) and one
        /// fresh folder per evaluation attempt. Evaluations run one at a time. Cancellation is cooperative: a running
        /// TasGenExecute finishes normally, then the run stops with <see cref="OptimisationOutcome.Cancelled"/>.
        /// </para>
        /// </summary>
        /// <param name="runsDirectory">Parent of the run folder; keep it short. Null for the default.</param>
        /// <param name="tasGenExecutePath">TasGenExecute.exe; null for the installed one (<see cref="Query.TasGenOptExecutePath"/>).</param>
        /// <param name="progress">Kernel progress, one notification per trace entry.</param>
        /// <param name="cancellationToken">Stops the run before the next evaluation.</param>
        public NativeGenOptRun RunNative(string runsDirectory = null, string tasGenExecutePath = null, System.IProgress<OptimisationProgress> progress = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {
            if (!IsValid())
            {
                throw new GenOptCompatibilityException("The TasGenExecute script is empty.");
            }

            string directory = GetDirectory();
            if (directory == null)
            {
                throw new System.IO.DirectoryNotFoundException("The optimisation workspace does not exist: '" + this.directory + "'.");
            }

            // Map and validate everything before any folder is created or any process is started.
            List<NumberParameter> numberParameters = Convert.NumberParameters(parameters);
            List<Objective> objectives = Convert.Objectives(objectiveFunctionLocation);
            OptimisationProblem problem = Convert.ToSAM_OptimisationProblem(numberParameters.ConvertAll(x => (IParameter)x), objectiveFunctionLocation);
            if (optimizationSettings == null)
            {
                throw new GenOptCompatibilityException("OptimizationSettings is required.");
            }

            Optimiser optimiser = Convert.ToSAM_Optimiser(algorithm, optimizationSettings, numberParameters.Count);

            string executablePath = string.IsNullOrWhiteSpace(tasGenExecutePath) ? Query.TasGenOptExecutePath() : tasGenExecutePath;
            if (!System.IO.File.Exists(executablePath))
            {
                throw new System.IO.FileNotFoundException("TasGenExecute was not found.", executablePath);
            }

            NativeGenOptWorkspace workspace = NativeGenOptWorkspace.Create(
                directory,
                script.Text,
                string.IsNullOrWhiteSpace(runsDirectory) ? System.IO.Path.Combine(directory, "SAM_NativeGenOpt") : runsDirectory);

            TasGenExecuteObjectiveEvaluator evaluator = new TasGenExecuteObjectiveEvaluator(executablePath, workspace.ProjectDirectory, workspace.EvaluationsDirectory, numberParameters, objectives);
            OptimisationResult result = optimiser.Run(problem, evaluator, progress, cancellationToken);

            return new NativeGenOptRun(workspace, numberParameters.ConvertAll(x => x.Name), objectives.ConvertAll(x => x.Name), result);
        }

        bool IsValid()
        {
            if(string.IsNullOrWhiteSpace(script?.Text))
            {
                return false;
            }

            return true;
        }

        public string Directory
        {
            get
            {
                return directory;
            }

            set
            {
                directory = value;
            }
        }

        public CommandFile CommandFile
        {
            get
            {
                return new CommandFile() { OptimizationSettings = optimizationSettings, Algorithm = algorithm, Parameters = parameters };
            }
        }

        public ConfigFile ConfigFile
        {
            get
            {
                Simulation simulation = new Simulation()
                {
                    Files = new Files(files[FileType.Template], files[FileType.Input], files[FileType.Log], files[FileType.Output], files[FileType.Configuration]),
                    ObjectiveFunctionLocation = objectiveFunctionLocation,
                };

                Optimization optimization = new Optimization()
                {
                    Files = new Files(files[FileType.Command]),
                };

                return new ConfigFile() { Simulation = simulation, Optimization = optimization };
            }
        }

        public TemplateFile TemplateFile
        {
            get
            {
                return new TemplateFile(parameters);
            }
        }

        public ParameterFile ParameterFile
        {
            get
            {
                return new ParameterFile(parameters);
            }
        }

        public ScriptFile ScriptFile
        {
            get
            {
                return new ScriptFile(script);
            }
        }

        public OutputFile OutputFile
        {
            get
            {
                return new OutputFile(objectiveFunctionLocation);
            }
        }

        public string GetPath(FileType fileType)
        {
            string directory = GetDirectory();
            if (string.IsNullOrWhiteSpace(directory) || !System.IO.Directory.Exists(directory))
            {
                return null;
            }

            string name = files?[fileType]?.Names?.FirstOrDefault();
            if(string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return System.IO.Path.Combine(directory, name);

        }

        public List<string> GetPaths(FileType fileType)
        {
            string directory = GetDirectory();
            if (string.IsNullOrWhiteSpace(directory) || !System.IO.Directory.Exists(directory))
            {
                return null;
            }

            IEnumerable<string> names = files?[fileType]?.Names;
            if(names == null)
            {
                return null;
            }

            List<string> result = new List<string>();
            foreach(string name in names)
            {
                result.Add(System.IO.Path.Combine(directory, name));
            }

            return result;
        }

        public bool SetFileName(FileType fileType, string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            files?.Clear(fileType);

            return AddFileName(fileType, fileName);
        }
        
        public bool AddFileName(FileType fileType, string fileName)
        {
            if(string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            if(files == null)
            {
                files = new Files();
            }

            return files.Add(fileType, fileName);
        }

        public bool AddParameter(string name, double initial, double min, double max, double step)
        {
            if(string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if(parameters == null)
            {
                parameters = new List<IParameter>();
            }

            IParameter parameter = parameters.Find(x => x.Name == name);
            if (parameter != null)
            {
                parameters.Remove(parameter);
            }

            parameters.Add(new NumberParameter() { Name = name, Initial = initial, Min = min, Max = max, Step = step });
            return true;
        }

        public bool AddParameter(IParameter parameter)
        {
            if (string.IsNullOrWhiteSpace(parameter?.Name))
            {
                return false;
            }

            if (parameters == null)
            {
                parameters = new List<IParameter>();
            }

            IParameter parameter_Existing = parameters.Find(x => x.Name == parameter.Name);
            if (parameter_Existing != null)
            {
                parameters.Remove(parameter_Existing);
            }

            parameters.Add(parameter);
            return true;
        }

        public bool AddObjective(Objective objective)
        {
            if (string.IsNullOrWhiteSpace(objective?.Name))
            {
                return false;
            }

            if (objectiveFunctionLocation == null)
            {
                objectiveFunctionLocation = new ObjectiveFunctionLocation();
            }

            return objectiveFunctionLocation.Add(objective);
        }

        public bool AddObjective(string name, string delimiter)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (objectiveFunctionLocation == null)
            {
                objectiveFunctionLocation = new ObjectiveFunctionLocation();
            }

            return objectiveFunctionLocation.Add(name, delimiter);
        }

        public bool AddObjective(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (objectiveFunctionLocation == null)
            {
                objectiveFunctionLocation = new ObjectiveFunctionLocation();
            }

            return objectiveFunctionLocation.Add(name);
        }

        public bool AddScript(string text)
        {
            script = new Script(text);

            return true;
        }

        public Algorithm Algorithm
        {
            get
            {
                return algorithm;
            }

            set
            {
                algorithm = value;
            }
        }

        /// <summary>The OptimizationSettings section (MaxIte, MaxEqualResults, ...).</summary>
        public OptimizationSettings OptimizationSettings
        {
            get
            {
                return optimizationSettings;
            }

            set
            {
                optimizationSettings = value;
            }
        }
    }
}
