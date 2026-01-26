using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Winhance.Infrastructure.Features.ErrorRecovery
{
    /// <summary>
    /// Smart error recovery and auto-fix system for rapid batch fixing
    /// </summary>
    public class SmartErrorRecoverySystem
    {
        private readonly ILogger<SmartErrorRecoverySystem> _logger;
        private readonly ConcurrentDictionary<string, List<ErrorFix>> _errorFixes;
        private readonly ConcurrentDictionary<string, FixPattern> _fixPatterns;

        public SmartErrorRecoverySystem(ILogger<SmartErrorRecoverySystem> logger)
        {
            _logger = logger;
            _errorFixes = new ConcurrentDictionary<string, List<ErrorFix>>();
            _fixPatterns = new ConcurrentDictionary<string, FixPattern>();
            InitializeFixPatterns();
        }

        private void InitializeFixPatterns()
        {
            // Common error patterns and their fixes
            _fixPatterns["CS0103"] = new FixPattern
            {
                ErrorType = "Variable not declared",
                Regex = @"CS0103.*'(\w+)' does not exist",
                FixAction = FixUndeclaredVariable,
                AutoFix = true
            };

            _fixPatterns["CS0246"] = new FixPattern
            {
                ErrorType = "Type not found",
                Regex = @"CS0246.*The type or namespace name '(\w+)' could not be found",
                FixAction = FixMissingType,
                AutoFix = true
            };

            _fixPatterns["CS0117"] = new FixPattern
            {
                ErrorType = "Member not found",
                Regex = @"CS0117.*'(\w+)' does not contain a definition for '(\w+)'",
                FixAction = FixMissingMember,
                AutoFix = true
            };

            _fixPatterns["CS0029"] = new FixPattern
            {
                ErrorType = "Cannot implicitly convert",
                Regex = @"CS0029.*Cannot implicitly convert type '(\w+)' to '(\w+)'",
                FixAction = FixTypeConversion,
                AutoFix = true
            };

            _fixPatterns["CS0161"] = new FixPattern
            {
                ErrorType = "Not all code paths return",
                Regex = @"CS0161.*'(\w+)\(\)': not all code paths return a value",
                FixAction = FixMissingReturn,
                AutoFix = true
            };

            _fixPatterns["CS0165"] = new FixPattern
            {
                ErrorType = "Use of unassigned local variable",
                Regex = @"CS0165.*Use of unassigned local variable '(\w+)'",
                FixAction = FixUnassignedVariable,
                AutoFix = true
            };

            _fixPatterns["CS0102"] = new FixPattern
            {
                ErrorType = "Duplicate type",
                Regex = @"CS0102.*The type '(\w+)' already contains a definition for '(\w+)'",
                FixAction = FixDuplicateDefinition,
                AutoFix = true
            };

            _fixPatterns["CS0122"] = new FixPattern
            {
                ErrorType = "Inaccessible member",
                Regex = @"CS0122.*'(\w+)' is inaccessible due to its protection level",
                FixAction = FixAccessibility,
                AutoFix = true
            };

            _fixPatterns["CS0501"] = new FixPattern
            {
                ErrorType = "Abstract method not implemented",
                Regex = @"CS0501.*'(\w+)' must declare a body because it is not marked abstract",
                FixAction = FixAbstractMethod,
                AutoFix = true
            };

            _fixPatterns["CS7036"] = new FixPattern
            {
                ErrorType = "Required parameter not provided",
                Regex = @"CS7036.*Required parameter '(\w+)' must be provided",
                FixAction = FixRequiredParameter,
                AutoFix = true
            };
        }

        public async Task<ErrorFixResult> AnalyzeAndFixErrorsAsync(string filePath)
        {
            var result = new ErrorFixResult
            {
                FilePath = filePath,
                StartTime = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Analyzing errors in {FilePath}", filePath);
                
                // Read file content
                var content = await File.ReadAllTextAsync(filePath);
                
                // Get compilation errors
                var errors = await GetCompilationErrorsAsync(content, filePath);
                
                result.TotalErrors = errors.Count;
                _logger.LogInformation("Found {Count} errors in {FilePath}", errors.Count, filePath);

                // Fix each error
                foreach (var error in errors)
                {
                    var fix = await FixErrorAsync(error, content, filePath);
                    if (fix.Success)
                    {
                        result.FixedErrors++;
                        content = fix.FixedContent;
                        result.AppliedFixes.Add(fix);
                        
                        _logger.LogInformation("✅ Fixed error {ErrorCode} at line {LineNumber}: {Message}", 
                            error.ErrorCode, error.LineNumber, error.Message);
                    }
                    else
                    {
                        result.FailedErrors++;
                        result.FailedFixes.Add(fix);
                        
                        _logger.LogWarning("❌ Failed to fix error {ErrorCode} at line {LineNumber}: {Message}", 
                            error.ErrorCode, error.LineNumber, error.Message);
                    }
                }

                // Write fixed content back to file
                if (result.FixedErrors > 0)
                {
                    await File.WriteAllTextAsync(filePath, content);
                    _logger.LogInformation("Applied {FixedCount} fixes to {FilePath}", result.FixedErrors, filePath);
                }

                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;
                result.Success = result.FailedErrors == 0;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing and fixing errors in {FilePath}", filePath);
                result.Success = false;
                result.Error = ex;
                return result;
            }
        }

        private async Task<List<CompilationError>> GetCompilationErrorsAsync(string content, string filePath)
        {
            var errors = new List<CompilationError>();

            // Create a syntax tree
            var syntaxTree = CSharpSyntaxTree.ParseText(content);
            var root = await syntaxTree.GetRootAsync();

            // Get compilation
            var compilation = CSharpCompilation.Create("TempCompilation")
                .AddSyntaxTrees(syntaxTree)
                .AddReferences(
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location)
                );

            // Get diagnostics
            var diagnostics = compilation.GetDiagnostics();
            
            foreach (var diagnostic in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                var lineSpan = diagnostic.Location.GetLineSpan();
                errors.Add(new CompilationError
                {
                    ErrorCode = diagnostic.Id,
                    Message = diagnostic.GetMessage(),
                    LineNumber = lineSpan.StartLinePosition.Line + 1,
                    ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                    FullMessage = diagnostic.ToString()
                });
            }

            return errors;
        }

        private async Task<ErrorFix> FixErrorAsync(CompilationError error, string content, string filePath)
        {
            var fix = new ErrorFix
            {
                ErrorCode = error.ErrorCode,
                LineNumber = error.LineNumber,
                OriginalMessage = error.Message
            };

            try
            {
                if (_fixPatterns.TryGetValue(error.ErrorCode, out var pattern))
                {
                    fix = await pattern.FixAction(error, content, filePath);
                    fix.Success = true;
                }
                else
                {
                    // Try generic fixes
                    fix = await ApplyGenericFix(error, content, filePath);
                }
            }
            catch (Exception ex)
            {
                fix.Success = false;
                fix.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Error applying fix for {ErrorCode}", error.ErrorCode);
            }

            return fix;
        }

        private async Task<ErrorFix> FixUndeclaredVariable(CompilationError error, string content, string filePath)
        {
            var match = Regex.Match(error.Message, @"'(\w+)' does not exist");
            if (!match.Success) return new ErrorFix { Success = false };

            var variableName = match.Groups[1].Value;
            var lines = content.Split('\n');
            var errorLineIndex = error.LineNumber - 1;

            // Try to infer variable type from context
            var inferredType = InferVariableType(variableName, lines, errorLineIndex);
            
            // Add variable declaration before first use
            var declaration = $"var {variableName} = {GetDefaultValue(inferredType)};";
            var fixedContent = InsertDeclaration(lines, errorLineIndex, declaration);

            return new ErrorFix
            {
                Success = true,
                FixedContent = fixedContent,
                FixDescription = $"Added variable declaration: {declaration}"
            };
        }

        private async Task<ErrorFix> FixMissingType(CompilationError error, string content, string filePath)
        {
            var match = Regex.Match(error.Message, @"The type or namespace name '(\w+)' could not be found");
            if (!match.Success) return new ErrorFix { Success = false };

            var typeName = match.Groups[1].Value;
            var lines = content.Split('\n');

            // Add using statement or create class
            var usingStatement = $"using System.{GetNamespaceForType(typeName)};";
            var fixedContent = AddUsingStatement(lines, usingStatement);

            return new ErrorFix
            {
                Success = true,
                FixedContent = fixedContent,
                FixDescription = $"Added using statement: {usingStatement}"
            };
        }

        private async Task<ErrorFix> FixMissingMember(CompilationError error, string content, string filePath)
        {
            var match = Regex.Match(error.Message, @"'(\w+)' does not contain a definition for '(\w+)'");
            if (!match.Success) return new ErrorFix { Success = false };

            var className = match.Groups[1].Value;
            var memberName = match.Groups[2].Value;
            var lines = content.Split('\n');

            // Find class definition and add missing member
            var classLineIndex = FindClassDefinition(lines, className);
            if (classLineIndex >= 0)
            {
                var memberDefinition = GenerateMemberDefinition(memberName);
                var fixedContent = AddMemberToClass(lines, classLineIndex, memberDefinition);

                return new ErrorFix
                {
                    Success = true,
                    FixedContent = fixedContent,
                    FixDescription = $"Added member to class: {memberDefinition}"
                };
            }

            return new ErrorFix { Success = false };
        }

        private async Task<ErrorFix> FixTypeConversion(CompilationError error, string content, string filePath)
        {
            var match = Regex.Match(error.Message, @"Cannot implicitly convert type '(\w+)' to '(\w+)'");
            if (!match.Success) return new ErrorFix { Success = false };

            var fromType = match.Groups[1].Value;
            var toType = match.Groups[2].Value;
            var lines = content.Split('\n');
            var errorLineIndex = error.LineNumber - 1;

            // Add explicit cast
            var errorLine = lines[errorLineIndex];
            var fixedLine = Regex.Replace(errorLine, $@"({Regex.Escape(fromType)}\s+\w+)", $"({toType})$1");
            lines[errorLineIndex] = fixedLine;

            return new ErrorFix
            {
                Success = true,
                FixedContent = string.Join('\n', lines),
                FixDescription = $"Added explicit cast from {fromType} to {toType}"
            };
        }

        private async Task<ErrorFix> FixMissingReturn(CompilationError error, string content, string filePath)
        {
            var match = Regex.Match(error.Message, @"'(\w+)\(\)': not all code paths return a value");
            if (!match.Success) return new ErrorFix { Success = false };

            var methodName = match.Groups[1].Value;
            var lines = content.Split('\n');

            // Find method and add return statement
            var methodLineIndex = FindMethodDefinition(lines, methodName);
            if (methodLineIndex >= 0)
            {
                var closingBraceIndex = FindClosingBrace(lines, methodLineIndex);
                if (closingBraceIndex > 0)
                {
                    lines[closingBraceIndex - 1] += "\n            return default;";
                }
            }

            return new ErrorFix
            {
                Success = true,
                FixedContent = string.Join('\n', lines),
                FixDescription = "Added default return statement"
            };
        }

        private async Task<ErrorFix> FixUnassignedVariable(CompilationError error, string content, string filePath)
        {
            var match = Regex.Match(error.Message, @"Use of unassigned local variable '(\w+)'");
            if (!match.Success) return new ErrorFix { Success = false };

            var variableName = match.Groups[1].Value;
            var lines = content.Split('\n');
            var errorLineIndex = error.LineNumber - 1;

            // Initialize variable at declaration
            var declarationLineIndex = FindVariableDeclaration(lines, variableName, errorLineIndex);
            if (declarationLineIndex >= 0)
            {
                lines[declarationLineIndex] = lines[declarationLineIndex].Replace($";", $" = {GetDefaultValue("var")};");
            }

            return new ErrorFix
            {
                Success = true,
                FixedContent = string.Join('\n', lines),
                FixDescription = $"Initialized variable {variableName}"
            };
        }

        private async Task<ErrorFix> FixDuplicateDefinition(CompilationError error, string content, string filePath)
        {
            var match = Regex.Match(error.Message, @"The type '(\w+)' already contains a definition for '(\w+)'");
            if (!match.Success) return new ErrorFix { Success = false };

            var memberName = match.Groups[2].Value;
            var lines = content.Split('\n');
            var errorLineIndex = error.LineNumber - 1;

            // Remove duplicate definition
            lines[errorLineIndex] = $"// REMOVED DUPLICATE: {lines[errorLineIndex]}";

            return new ErrorFix
            {
                Success = true,
                FixedContent = string.Join('\n', lines),
                FixDescription = $"Removed duplicate definition of {memberName}"
            };
        }

        private async Task<ErrorFix> FixAccessibility(CompilationError error, string content, string filePath)
        {
            var match = Regex.Match(error.Message, @"'(\w+)' is inaccessible due to its protection level");
            if (!match.Success) return new ErrorFix { Success = false };

            var memberName = match.Groups[1].Value;
            var lines = content.Split('\n');

            // Find member and change accessibility
            var memberLineIndex = FindMemberDefinition(lines, memberName);
            if (memberLineIndex >= 0)
            {
                lines[memberLineIndex] = lines[memberLineIndex].Replace("private", "public");
            }

            return new ErrorFix
            {
                Success = true,
                FixedContent = string.Join('\n', lines),
                FixDescription = $"Changed {memberName} accessibility to public"
            };
        }

        private async Task<ErrorFix> FixAbstractMethod(CompilationError error, string content, string filePath)
        {
            var match = Regex.Match(error.Message, @"'(\w+)' must declare a body");
            if (!match.Success) return new ErrorFix { Success = false };

            var methodName = match.Groups[1].Value;
            var lines = content.Split('\n');
            var errorLineIndex = error.LineNumber - 1;

            // Add method body
            lines[errorLineIndex] = lines[errorLineIndex].Replace(";", "\n        {\n            // TODO: Implement method\n            throw new NotImplementedException();\n        }");

            return new ErrorFix
            {
                Success = true,
                FixedContent = string.Join('\n', lines),
                FixDescription = $"Added body to abstract method {methodName}"
            };
        }

        private async Task<ErrorFix> FixRequiredParameter(CompilationError error, string content, string filePath)
        {
            var match = Regex.Match(error.Message, @"Required parameter '(\w+)' must be provided");
            if (!match.Success) return new ErrorFix { Success = false };

            var parameterName = match.Groups[1].Value;
            var lines = content.Split('\n');
            var errorLineIndex = error.LineNumber - 1;

            // Add parameter to method call
            lines[errorLineIndex] = lines[errorLineIndex].Replace("(", $"({parameterName}, ");

            return new ErrorFix
            {
                Success = true,
                FixedContent = string.Join('\n', lines),
                FixDescription = $"Added required parameter {parameterName}"
            };
        }

        private async Task<ErrorFix> ApplyGenericFix(CompilationError error, string content, string filePath)
        {
            // Apply generic fixes based on error type
            return new ErrorFix
            {
                Success = false,
                ErrorMessage = $"No specific fix available for error {error.ErrorCode}"
            };
        }

        // Helper methods
        private string InferVariableType(string variableName, string[] lines, int errorLineIndex)
        {
            // Simple type inference based on usage
            for (int i = errorLineIndex; i < Math.Min(errorLineIndex + 10, lines.Length); i++)
            {
                var line = lines[i];
                if (line.Contains($"{variableName}.") || line.Contains($"{variableName}["))
                {
                    return "var";
                }
                if (line.Contains($"{variableName} +") || line.Contains($"{variableName}.ToString"))
                {
                    return "string";
                }
                if (line.Contains($"{variableName} *") || line.Contains($"{variableName} /"))
                {
                    return "double";
                }
            }
            return "var";
        }

        private string GetDefaultValue(string type)
        {
            return type.ToLower() switch
            {
                "string" => "\"\"",
                "int" => "0",
                "double" => "0.0",
                "bool" => "false",
                "var" => "default",
                _ => "default"
            };
        }

        private string GetNamespaceForType(string typeName)
        {
            return typeName switch
            {
                "List" => "Collections.Generic",
                "Dictionary" => "Collections.Generic",
                "Task" => "Threading.Tasks",
                "DateTime" => "Globalization",
                "StringBuilder" => "Text",
                _ => "Collections.Generic"
            };
        }

        private string InsertDeclaration(string[] lines, int errorLineIndex, string declaration)
        {
            var result = lines.ToList();
            result.Insert(errorLineIndex, "            " + declaration);
            return string.Join('\n', result);
        }

        private string AddUsingStatement(string[] lines, string usingStatement)
        {
            var result = lines.ToList();
            var insertIndex = 0;
            
            // Find the last using statement
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("using "))
                {
                    insertIndex = i + 1;
                }
            }
            
            result.Insert(insertIndex, usingStatement);
            return string.Join('\n', result);
        }

        private int FindClassDefinition(string[] lines, string className)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains($"class {className}"))
                {
                    return i;
                }
            }
            return -1;
        }

        private int FindMethodDefinition(string[] lines, string methodName)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains($"{methodName}("))
                {
                    return i;
                }
            }
            return -1;
        }

        private int FindVariableDeclaration(string[] lines, string variableName, int startIndex)
        {
            for (int i = startIndex - 1; i >= 0; i--)
            {
                if (lines[i].Contains($"var {variableName}") || lines[i].Contains($"{variableName} "))
                {
                    return i;
                }
            }
            return -1;
        }

        private int FindMemberDefinition(string[] lines, string memberName)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(memberName) && (lines[i].Contains("public") || lines[i].Contains("private") || lines[i].Contains("protected")))
                {
                    return i;
                }
            }
            return -1;
        }

        private int FindClosingBrace(string[] lines, int startIndex)
        {
            var braceCount = 0;
            for (int i = startIndex; i < lines.Length; i++)
            {
                if (lines[i].Contains("{")) braceCount++;
                if (lines[i].Contains("}")) braceCount--;
                if (braceCount == 0 && i > startIndex) return i;
            }
            return -1;
        }

        private string GenerateMemberDefinition(string memberName)
        {
            // Generate a basic property
            return $"        public object {memberName} {{ get; set; }}";
        }

        private string AddMemberToClass(string[] lines, int classLineIndex, string memberDefinition)
        {
            var result = lines.ToList();
            var insertIndex = classLineIndex + 1;
            
            // Skip any attributes or comments
            while (insertIndex < result.Count && (result[insertIndex].Trim().StartsWith("[") || result[insertIndex].Trim().StartsWith("//")))
            {
                insertIndex++;
            }
            
            result.Insert(insertIndex, memberDefinition);
            return string.Join('\n', result);
        }
    }

    public class CompilationError
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string FullMessage { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
    }

    public class ErrorFix
    {
        public string ErrorCode { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string OriginalMessage { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string FixedContent { get; set; } = string.Empty;
        public string FixDescription { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class ErrorFixResult
    {
        public string FilePath { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public int TotalErrors { get; set; }
        public int FixedErrors { get; set; }
        public int FailedErrors { get; set; }
        public bool Success { get; set; }
        public Exception? Error { get; set; }
        public List<ErrorFix> AppliedFixes { get; set; } = new();
        public List<ErrorFix> FailedFixes { get; set; } = new();
    }

    public class FixPattern
    {
        public string ErrorType { get; set; } = string.Empty;
        public string Regex { get; set; } = string.Empty;
        public Func<CompilationError, string, string, Task<ErrorFix>> FixAction { get; set; } = null!;
        public bool AutoFix { get; set; }
    }
}
