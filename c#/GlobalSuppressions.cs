// ReSharper disable once RedundantUsingDirective
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("ApiDesign", "SS036:An enum should explicitly specify its values", Justification = "<Pending>")]
[assembly: SuppressMessage("ApiDesign", "SS039:An enum should specify a default value", Justification = "<Pending>")]
[assembly: SuppressMessage("Class Design", "AV1008:Class should not be static")]
[assembly: SuppressMessage("Class Design", "AV1010:Member hides inherited member")]
[assembly: SuppressMessage("Correctness", "SS019:Switch should have default label.")]
[assembly: SuppressMessage("Critical Bug", "S6674:Log message template should be syntactically correct")]
[assembly: SuppressMessage("Design", "CC0021:Use nameof")]
[assembly: SuppressMessage("Design", "CC0031:Check for null before calling a delegate")]
[assembly: SuppressMessage("Design", "CC0091:Use static method", Justification = "https://github.com/code-cracker/code-cracker/issues/1087")]
[assembly: SuppressMessage("Design", "CC0120:Your Switch maybe include default clause")]
[assembly: SuppressMessage("Design", "MA0048:File name must match type name")]
[assembly: SuppressMessage("Design", "MA0051:Method is too long")]
[assembly: SuppressMessage("Design", "MA0076:Do not use implicit culture-sensitive ToString in interpolated strings", Justification = "https://stackoverflow.com/questions/8492449/is-int32-tostring-culture-specific")]
[assembly: SuppressMessage("Documentation", "AV2305:Missing XML comment for internally visible type, member or parameter")]
[assembly: SuppressMessage("Framework", "AV2220:Simple query should be replaced by extension method call")]
[assembly: SuppressMessage("Maintainability", "AV1500:Member or local function contains too many statements")]
[assembly: SuppressMessage("Maintainability", "AV1507:File contains multiple types")]
[assembly: SuppressMessage("Maintainability", "AV1532:Loop statement contains nested loop")]
[assembly: SuppressMessage("Maintainability", "AV1535:Missing block in case or default clause of switch statement")]
[assembly: SuppressMessage("Maintainability", "AV1537:If-else-if construct should end with an unconditional else clause")]
[assembly: SuppressMessage("Maintainability", "AV1554:Method contains optional parameter in type hierarchy")]
[assembly: SuppressMessage("Maintainability", "AV1555:Avoid using non-(nullable-)boolean named arguments")]
[assembly: SuppressMessage("Maintainability", "AV1561:Signature contains too many parameters")]
[assembly: SuppressMessage("Maintainability", "AV1562:Do not declare a parameter as ref or out")]
[assembly: SuppressMessage("Maintainability", "AV1564:Parameter in public or internal member is of type bool or bool?")]
[assembly: SuppressMessage("Maintainability", "AV1580:Method argument calls a nested method")]
[assembly: SuppressMessage("Maintainability", "CC0097:You have missing/unexistent parameters in Xml Docs")]
[assembly: SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out")]
[assembly: SuppressMessage("Minor Code Smell", "S3604:Member initializer values should not be redundant")]
[assembly: SuppressMessage("Miscellaneous Design", "AV1210:Catch a specific exception instead of Exception, SystemException or ApplicationException")]
[assembly: SuppressMessage("Naming", "AV1704:Identifier contains one or more digits in its name")]
[assembly: SuppressMessage("Naming", "AV1706:Identifier contains an abbreviation or is too short")]
[assembly: SuppressMessage("Naming", "AV1745:Name of extension method container class should end with 'Extensions'")]
[assembly: SuppressMessage("Naming", "AV1755:Name of async method should end with Async or TaskAsync")]
[assembly: SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix")]
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates")]
[assembly: SuppressMessage("Roslynator", "RCS1001:Add braces (when expression spans over multiple lines).")]
[assembly: SuppressMessage("Roslynator", "RCS1139:Add summary element to documentation comment.")]
[assembly: SuppressMessage("Roslynator", "RCS1156:Use string.Length instead of comparison with empty string.")]
[assembly: SuppressMessage("Style", "CC0001:You should use 'var' whenever possible.")]
[assembly: SuppressMessage("Style", "CC0037:Remove commented code.")]
[assembly: SuppressMessage("Style", "CC0061:Asynchronous method can be terminated with the 'Async' keyword.")]
[assembly: SuppressMessage("Style", "MA0003:Add parameter name to improve readability")]
[assembly: SuppressMessage("Style", "MA0007:Add a comma after the last value")]
[assembly: SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods")]
[assembly: SuppressMessage("Usage", "CC0057:Unused parameters")]
[assembly: SuppressMessage("Usage", "MA0001:StringComparison is missing")]
[assembly: SuppressMessage("Usage", "MA0002:IEqualityComparer<string> or IComparer<string> is missing", Justification = "https://stackoverflow.com/questions/56478995/default-stringcomparer-used-by-dictionarystring-t")]
[assembly: SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait")]
[assembly: SuppressMessage("Usage", "MA0006:Use String.Equals instead of equality operator")]
[assembly: SuppressMessage("Usage", "MA0015:Specify the parameter name in ArgumentException")]

[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:Prefix local calls with this")]
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:Elements should be documented")]
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1601:Partial elements should be documented")]
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1602:Enumeration items should be documented")]
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1604:Element documentation should have summary")]
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters should be documented")]
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:Element return value should be documented")]
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1629:Documentation text should end with a period")]
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1633:File should have header")]
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1500:Braces for multi-line statements should not share line")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1502:Element should not be on a single line")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1503:Braces should not be omitted")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1513:Closing brace should be followed by blank line")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1516:Elements should be separated by blank line")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1519:Braces should not be omitted from multi-line child statement")]
[assembly: SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type")]
[assembly: SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1404:Code analysis suppression should have justification")]
[assembly: SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1413:Use trailing comma in multi-line initializers")]
[assembly: SuppressMessage("StyleCop.CSharp.NamingRules", "SA1309:Field names should not begin with underscore")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:Prefix local calls with this")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1108:Block statements should not contain embedded comments")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1110:Opening parenthesis or bracket should be on declaration line")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1116:Split parameters should start on line after declaration")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1117:Parameters should be on same line or separate lines")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1121:Use built-in type alias")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1122:Use string.Empty for empty strings")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1127:Generic type constraints should be on their own line")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1128:Put constructor initializers on their own line")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1133:Each attribute should be placed on its own line of code")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1134:Attributes should not share line")]
[assembly: SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1008:Opening parenthesis should be spaced correctly")]
[assembly: SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1009:Closing parenthesis should be spaced correctly")]
[assembly: SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1012:Opening braces should be spaced correctly")]
[assembly: SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1013:Closing braces should be spaced correctly")]
[assembly: SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1015:Closing generic brackets should be spaced correctly", Justification = "https://github.com/DotNetAnalyzers/StyleCopAnalyzers/issues/3856")]
