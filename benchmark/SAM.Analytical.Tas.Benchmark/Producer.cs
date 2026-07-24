// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.IO;
using System.Linq;
using SAM.Analytical.Benchmark;

namespace SAM.Analytical.Tas.Benchmark
{
    /// <summary>
    /// The COM-free core steps of the TAS producer that carry decisions worth unit-testing: loading
    /// the source model and the validate/write/exit-code decision. These use only the B1a schema and
    /// SAM core, so they run offline with no TAS install. The CLI (<c>Program</c>) composes these
    /// with the shared host and the TAS run.
    /// </summary>
    public static class Producer
    {
        /// <summary>
        /// Loads the source model. A file that exists but does not carry a SAM AnalyticalModel —
        /// whether it deserializes to nothing or the SAM deserializer throws part way through — is a
        /// deserialization failure, reported as <see cref="System.Text.Json.JsonException"/> so the
        /// shared host maps it to input/IO/serialization (exit 3), never a producer failure (5).
        /// </summary>
        public static AnalyticalModel LoadModel(string modelPath)
        {
            AnalyticalModel model;
            try
            {
                model = Core.Convert.ToSAM<AnalyticalModel>(modelPath)?.FirstOrDefault();
            }
            catch (Exception exception) when (!(exception is IOException) && !(exception is UnauthorizedAccessException) && !(exception is System.Text.Json.JsonException))
            {
                throw new System.Text.Json.JsonException("The model file could not be read as a SAM AnalyticalModel: " + modelPath, exception);
            }

            if (model == null)
            {
                throw new System.Text.Json.JsonException("The model file did not deserialize to a SAM AnalyticalModel: " + modelPath);
            }

            return model;
        }

        /// <summary>
        /// Validates, writes and reports a produced document, returning the shared exit code: a
        /// document that fails schema validation throws <see cref="BenchmarkValidationException"/>
        /// (the host maps it to <see cref="BenchmarkExitCode.ValidationFailure"/>); a valid document
        /// whose run did not succeed returns <see cref="BenchmarkExitCode.ProducerFailure"/>;
        /// otherwise <see cref="BenchmarkExitCode.Success"/>.
        /// </summary>
        public static int Emit(BenchmarkDocument document, string outputPath, bool runSucceeded, TextWriter standardOutput)
        {
            BenchmarkValidationResult validation = BenchmarkValidator.Validate(document);
            if (!validation.IsValid)
            {
                throw new BenchmarkValidationException(validation);
            }

            BenchmarkSerializer.Write(outputPath, document);
            standardOutput?.WriteLine("Wrote " + outputPath + " (state=" + (runSucceeded ? RunState.Success : RunState.Failure) + ", route=Native-TAS).");
            return runSucceeded ? (int)BenchmarkExitCode.Success : (int)BenchmarkExitCode.ProducerFailure;
        }
    }
}
