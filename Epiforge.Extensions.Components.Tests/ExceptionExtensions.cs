using System.Reflection.Emit;

namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class ExceptionExtensions
{
    [TestMethod]
    public void GetFullDetailsInJson()
    {
        string fullDetails;
        try
        {
            throw new Exception("Test Exception");
        }
        catch (Exception ex)
        {
            fullDetails = ex.GetFullDetails(ExceptionFullDetailsFormat.Json);
        }
        var newLine = Regex.Escape(Environment.NewLine);
        var match = Regex.Match(fullDetails, $@"^{{{newLine}  ""type"": ""System.Exception"",{newLine}  ""message"": ""Test Exception"",{newLine}  ""properties"": {{{newLine}    ""TargetSite"": ""Void GetFullDetailsInJson\(\)"",{newLine}    ""HelpLink"": null,{newLine}    ""Source"": ""Epiforge\.Extensions\.Components\.Tests"",{newLine}    ""HResult"": -?\d+{newLine}  }},{newLine}  ""stackTrace"": \[{newLine}    {{{newLine}      ""type"": ""Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions"",{newLine}      ""method"": ""GetFullDetailsInJson\(\)""(,{newLine}      ""file"": "".*?Epiforge\.Extensions\.Components\.Tests\\\\ExceptionExtensions\.cs"",{newLine}      ""line"": \d+,{newLine}      ""column"": \d+)?,{newLine}      ""offset"": \d+{newLine}    }}{newLine}  ]{newLine}}}$");
        Assert.IsNotNull(match);
        Assert.IsTrue(match.Success);
    }

    [TestMethod]
    public void GetFullDetailsInJsonWithData()
    {
        string fullDetails;
        try
        {
            throw new Exception("Test Exception");
        }
        catch (Exception ex)
        {
            ex.Data["key"] = "value";
            fullDetails = ex.GetFullDetails(ExceptionFullDetailsFormat.Json);
        }
        var newLine = Regex.Escape(Environment.NewLine);
        var match = Regex.Match(fullDetails, $@"^{{{newLine}  ""type"": ""System.Exception"",{newLine}  ""message"": ""Test Exception"",{newLine}  ""data"": \[{newLine}    {{{newLine}      ""key"": ""key"",{newLine}      ""value"": ""value""{newLine}    }}{newLine}  ],{newLine}  ""properties"": {{{newLine}    ""TargetSite"": ""Void GetFullDetailsInJsonWithData\(\)"",{newLine}    ""HelpLink"": null,{newLine}    ""Source"": ""Epiforge\.Extensions\.Components\.Tests"",{newLine}    ""HResult"": -?\d+{newLine}  }},{newLine}  ""stackTrace"": \[{newLine}    {{{newLine}      ""type"": ""Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions"",{newLine}      ""method"": ""GetFullDetailsInJsonWithData\(\)""(,{newLine}      ""file"": "".*?Epiforge\.Extensions\.Components\.Tests\\\\ExceptionExtensions\.cs"",{newLine}      ""line"": \d+,{newLine}      ""column"": \d+)?,{newLine}      ""offset"": \d+{newLine}    }}{newLine}  ]{newLine}}}$");
        Assert.IsNotNull(match);
        Assert.IsTrue(match.Success);
    }

    [TestMethod]
    public void GetFullDetailsInJsonOfAggregateException()
    {
        string fullDetails;
        try
        {
            try
            {
                throw new Exception("Test Exception");
            }
            catch (Exception ex)
            {
                throw new AggregateException(ex);
            }
        }
        catch (Exception ex)
        {
            fullDetails = ex.GetFullDetails(ExceptionFullDetailsFormat.Json);
        }
        var newLine = Regex.Escape(Environment.NewLine);
        var match = Regex.Match(fullDetails, $@"^{{{newLine}  ""type"": ""System\.AggregateException"",{newLine}  ""message"": ""One or more errors occurred\.( \(Test Exception\))?"",{newLine}  ""properties"": {{{newLine}    ""TargetSite"": ""Void GetFullDetailsInJsonOfAggregateException\(\)"",{newLine}    ""HelpLink"": null,{newLine}    ""Source"": ""Epiforge\.Extensions\.Components\.Tests"",{newLine}    ""HResult"": -\d+{newLine}  }},{newLine}  ""stackTrace"": \[{newLine}    {{{newLine}      ""type"": ""Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions"",{newLine}      ""method"": ""GetFullDetailsInJsonOfAggregateException\(\)"",({newLine}      ""file"": "".*?\\\\Epiforge\.Extensions\.Components\.Tests\\\\ExceptionExtensions\.cs"",{newLine}      ""line"": \d+,{newLine}      ""column"": \d+,)?{newLine}      ""offset"": \d+{newLine}    }}{newLine}  \],{newLine}  ""innerExceptions"": \[{newLine}    {{{newLine}      ""type"": ""System\.Exception"",{newLine}      ""message"": ""Test Exception"",{newLine}      ""properties"": {{{newLine}        ""TargetSite"": ""Void GetFullDetailsInJsonOfAggregateException\(\)"",{newLine}        ""HelpLink"": null,{newLine}        ""Source"": ""Epiforge\.Extensions\.Components\.Tests"",{newLine}        ""HResult"": -\d+{newLine}      }},{newLine}      ""stackTrace"": \[{newLine}        {{{newLine}          ""type"": ""Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions"",{newLine}          ""method"": ""GetFullDetailsInJsonOfAggregateException\(\)""(,{newLine}          ""file"": "".*?\\\\Epiforge\.Extensions\.Components\.Tests\\\\ExceptionExtensions\.cs"",{newLine}          ""line"": \d+,{newLine}          ""column"": \d+)?,{newLine}          ""offset"": \d+{newLine}        }}{newLine}      \]{newLine}    }}{newLine}  \]{newLine}}}$");
        Assert.IsNotNull(match);
        Assert.IsTrue(match.Success);
    }

    [TestMethod]
    public void GetFullDetailsInJsonOfReflectionTypeLoadException()
    {
        string fullDetails;
        try
        {
            try
            {
                throw new Exception("Test Exception");
            }
            catch (Exception ex)
            {
                throw new ReflectionTypeLoadException(new Type?[] { GetType() }, new Exception?[] { ex });
            }
        }
        catch (Exception ex)
        {
            fullDetails = ex.GetFullDetails(ExceptionFullDetailsFormat.Json);
        }
        var newLine = Regex.Escape(Environment.NewLine);
        var match = Regex.Match(fullDetails, $@"^{{{newLine}  ""type"": ""System\.Reflection\.ReflectionTypeLoadException"",{newLine}  ""message"": ""Exception of type \\u0027System\.Reflection\.ReflectionTypeLoadException\\u0027 was thrown\.((\\r\\n|\\r|\\n)Test Exception)?"",{newLine}  ""properties"": {{{newLine}    ""TargetSite"": ""Void GetFullDetailsInJsonOfReflectionTypeLoadException\(\)"",{newLine}    ""HelpLink"": null,{newLine}    ""Source"": ""Epiforge\.Extensions\.Components\.Tests"",{newLine}    ""HResult"": -?\d+{newLine}  }},{newLine}  ""stackTrace"": \[{newLine}    {{{newLine}      ""type"": ""Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions"",{newLine}      ""method"": ""GetFullDetailsInJsonOfReflectionTypeLoadException\(\)""(,{newLine}      ""file"": "".*?\\\\Epiforge\.Extensions\.Components\.Tests\\\\ExceptionExtensions\.cs"",{newLine}      ""line"": \d+,{newLine}      ""column"": \d+)?,{newLine}      ""offset"": \d+{newLine}    }}{newLine}  \],{newLine}  ""loaderExceptions"": \[{newLine}    {{{newLine}      ""type"": ""System\.Exception"",{newLine}      ""message"": ""Test Exception"",{newLine}      ""properties"": {{{newLine}        ""TargetSite"": ""Void GetFullDetailsInJsonOfReflectionTypeLoadException\(\)"",{newLine}        ""HelpLink"": null,{newLine}        ""Source"": ""Epiforge\.Extensions\.Components\.Tests"",{newLine}        ""HResult"": -?\d+{newLine}      }},{newLine}      ""stackTrace"": \[{newLine}        {{{newLine}          ""type"": ""Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions"",{newLine}          ""method"": ""GetFullDetailsInJsonOfReflectionTypeLoadException\(\)""(,{newLine}          ""file"": "".*?\\\\Epiforge\.Extensions\.Components\.Tests\\\\ExceptionExtensions\.cs"",{newLine}          ""line"": \d+,{newLine}          ""column"": \d+)?,{newLine}          ""offset"": \d+{newLine}        }}{newLine}      \]{newLine}    }}{newLine}  \],{newLine}  ""types"": \[{newLine}    ""Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions""{newLine}  \]{newLine}}}$");
        Assert.IsNotNull(match);
        Assert.IsTrue(match.Success);
    }

    [TestMethod]
    public void GetFullDetailsInPlainText()
    {
        string fullDetails;
        try
        {
            throw new Exception("Test Exception");
        }
        catch (Exception ex)
        {
            fullDetails = ex.GetFullDetails();
        }
        var newLine = Regex.Escape(Environment.NewLine);
        var match = Regex.Match(fullDetails, $@"^-- Exception: Test Exception{newLine}   \.TargetSite = Void GetFullDetailsInPlainText\(\){newLine}   \.HelpLink = null{newLine}   \.Source = ""Epiforge\.Extensions\.Components\.Tests""{newLine}   \.HResult = -?\d+{newLine}   at void Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions\.GetFullDetailsInPlainText\(\)( in .*?Epiforge\.Extensions\.Components\.Tests/ExceptionExtensions\.cs:line \d+)?$");
        Assert.IsNotNull(match);
        Assert.IsTrue(match.Success);
    }

    [TestMethod]
    public void GetFullDetailsInPlainTextOfAggregateException()
    {
        string fullDetails;
        try
        {
            try
            {
                throw new Exception("Test Exception");
            }
            catch (Exception ex)
            {
                throw new AggregateException(ex);
            }
        }
        catch (Exception ex)
        {
            fullDetails = ex.GetFullDetails();
        }
        var newLine = Regex.Escape(Environment.NewLine);
        var match = Regex.Match(fullDetails, $@"^-- AggregateException: One or more errors occurred\.( \(Test Exception\))?{newLine}   \.TargetSite = Void GetFullDetailsInPlainTextOfAggregateException\(\){newLine}   \.HelpLink = null{newLine}   \.Source = ""Epiforge\.Extensions\.Components\.Tests""{newLine}   \.HResult = -?\d+{newLine}   at void Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions\.GetFullDetailsInPlainTextOfAggregateException\(\)( in .*?/Epiforge\.Extensions\.Components\.Tests/ExceptionExtensions\.cs:line \d+)?{newLine}{newLine}   -- Exception: Test Exception{newLine}      \.TargetSite = Void GetFullDetailsInPlainTextOfAggregateException\(\){newLine}      \.HelpLink = null{newLine}      \.Source = ""Epiforge\.Extensions\.Components\.Tests""{newLine}      \.HResult = -?\d+{newLine}      at void Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions\.GetFullDetailsInPlainTextOfAggregateException\(\)( in .*?Epiforge\.Extensions\.Components\.Tests/ExceptionExtensions.cs:line \d+)?$");
        Assert.IsNotNull(match);
        Assert.IsTrue(match.Success);
    }

    [TestMethod]
    public void GetFullDetailsInPlainTextOfReflectionTypeLoadException()
    {
        string fullDetails;
        try
        {
            try
            {
                throw new Exception("Test Exception");
            }
            catch (Exception ex)
            {
                throw new ReflectionTypeLoadException(new Type?[] { GetType() }, new Exception?[] { ex });
            }
        }
        catch (Exception ex)
        {
            fullDetails = ex.GetFullDetails();
        }
        var newLine = Regex.Escape(Environment.NewLine);
        var match = Regex.Match(fullDetails, $@"-- ReflectionTypeLoadException: Exception of type 'System\.Reflection\.ReflectionTypeLoadException' was thrown\.({newLine}   Test Exception)?{newLine}   Types: Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions{newLine}   \.TargetSite = Void GetFullDetailsInPlainTextOfReflectionTypeLoadException\(\){newLine}   \.HelpLink = null{newLine}   \.Source = ""Epiforge\.Extensions\.Components\.Tests""{newLine}   \.HResult = -\d+{newLine}   at void Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions\.GetFullDetailsInPlainTextOfReflectionTypeLoadException\(\)( in .*?/Epiforge\.Extensions\.Components\.Tests/ExceptionExtensions\.cs:line \d+)?{newLine}{newLine}   -- Exception: Test Exception{newLine}      \.TargetSite = Void GetFullDetailsInPlainTextOfReflectionTypeLoadException\(\){newLine}      \.HelpLink = null{newLine}      \.Source = ""Epiforge\.Extensions\.Components\.Tests""{newLine}      \.HResult = -\d+{newLine}      at Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions\.GetFullDetailsInPlainTextOfReflectionTypeLoadException\(\)( in .*?\\Epiforge\.Extensions\.Components\.Tests\\ExceptionExtensions\.cs:line \d+)?$");
        Assert.IsNotNull(match);
        Assert.IsTrue(match.Success);
    }

    [TestMethod]
    public void GetFullDetailsInPlainTextWithData()
    {
        string fullDetails;
        try
        {
            throw new Exception("Test Exception");
        }
        catch (Exception ex)
        {
            ex.Data["key"] = "value";
            fullDetails = ex.GetFullDetails();
        }
        var newLine = Regex.Escape(Environment.NewLine);
        var match = Regex.Match(fullDetails, $@"^-- Exception: Test Exception{newLine}   \.Data\[""key""] = ""value""{newLine}   \.TargetSite = Void GetFullDetailsInPlainTextWithData\(\){newLine}   \.HelpLink = null{newLine}   \.Source = ""Epiforge\.Extensions\.Components\.Tests""{newLine}   \.HResult = -?\d+{newLine}   at void Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions\.GetFullDetailsInPlainTextWithData\(\)( in .*?Epiforge\.Extensions\.Components\.Tests/ExceptionExtensions\.cs:line \d+)?$");
        Assert.IsNotNull(match);
        Assert.IsTrue(match.Success);
    }

    [TestMethod]
    public void GetFullDetailsInXml()
    {
        string fullDetails;
        try
        {
            throw new Exception("Test Exception");
        }
        catch (Exception ex)
        {
            fullDetails = ex.GetFullDetails(ExceptionFullDetailsFormat.Xml);
        }
        var newLine = Regex.Escape(Environment.NewLine);
        var match = Regex.Match(fullDetails, $@"^<exception{newLine}  type=""System\.Exception""{newLine}  message=""Test Exception"">{newLine}  <property{newLine}    name=""TargetSite""{newLine}    value=""Void GetFullDetailsInXml\(\)"" />{newLine}  <property{newLine}    name=""HelpLink"" />{newLine}  <property{newLine}    name=""Source""{newLine}    value=""Epiforge\.Extensions\.Components\.Tests"" />{newLine}  <property{newLine}    name=""HResult""{newLine}    value=""-?\d+"" />{newLine}  <stackTrace>{newLine}    <frame{newLine}      type=""Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions""{newLine}      method=""GetFullDetailsInXml\(\)""({newLine}      file="".*?Epiforge\.Extensions\.Components\.Tests\\ExceptionExtensions.cs""{newLine}      line=""\d+""{newLine}      column=""\d+"")?{newLine}      offset=""\d+"" />{newLine}  </stackTrace>{newLine}</exception>$");
        Assert.IsNotNull(match);
        Assert.IsTrue(match.Success);
    }

    [TestMethod]
    public void GetFullDetailsInXmlOfAggregateException()
    {
        string fullDetails;
        try
        {
            try
            {
                throw new Exception("Test Exception");
            }
            catch (Exception ex)
            {
                throw new AggregateException(ex);
            }
        }
        catch (Exception ex)
        {
            fullDetails = ex.GetFullDetails(ExceptionFullDetailsFormat.Xml);
        }
        var newLine = Regex.Escape(Environment.NewLine);
        var match = Regex.Match(fullDetails, $@"^<exception{newLine}  type=""System\.AggregateException""{newLine}  message=""One or more errors occurred.( \(Test Exception\))?"">{newLine}  <property{newLine}    name=""TargetSite""{newLine}    value=""Void GetFullDetailsInXmlOfAggregateException\(\)"" />{newLine}  <property{newLine}    name=""HelpLink"" />{newLine}  <property{newLine}    name=""Source""{newLine}    value=""Epiforge\.Extensions\.Components\.Tests"" />{newLine}  <property{newLine}    name=""HResult""{newLine}    value=""-?\d+"" />{newLine}  <stackTrace>{newLine}    <frame{newLine}      type=""Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions""{newLine}      method=""GetFullDetailsInXmlOfAggregateException\(\)""({newLine}      file="".*?\\Epiforge\.Extensions\.Components\.Tests\\ExceptionExtensions.cs""{newLine}      line=""\d+""{newLine}      column=""\d+"")?{newLine}      offset=""\d+"" />{newLine}  </stackTrace>{newLine}  <innerException{newLine}    type=""System\.Exception""{newLine}    message=""Test Exception"">{newLine}    <property{newLine}      name=""TargetSite""{newLine}      value=""Void GetFullDetailsInXmlOfAggregateException\(\)"" />{newLine}    <property{newLine}      name=""HelpLink"" />{newLine}    <property{newLine}      name=""Source""{newLine}      value=""Epiforge\.Extensions\.Components\.Tests"" />{newLine}    <property{newLine}      name=""HResult""{newLine}      value=""-?\d+"" />{newLine}    <stackTrace>{newLine}      <frame{newLine}        type=""Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions""{newLine}        method=""GetFullDetailsInXmlOfAggregateException\(\)""({newLine}        file="".*?\\Epiforge\.Extensions\.Components\.Tests\\ExceptionExtensions\.cs""{newLine}        line=""\d+""{newLine}        column=""\d+"")?{newLine}        offset=""\d+"" />{newLine}    </stackTrace>{newLine}  </innerException>{newLine}</exception>$");
        Assert.IsNotNull(match);
        Assert.IsTrue(match.Success);
    }

    [TestMethod]
    public void GetFullDetailsInXmlOfReflectionTypeLoadException()
    {
        string fullDetails;
        try
        {
            try
            {
                throw new Exception("Test Exception");
            }
            catch (Exception ex)
            {
                throw new ReflectionTypeLoadException(new Type?[] { GetType() }, new Exception?[] { ex });
            }
        }
        catch (Exception ex)
        {
            fullDetails = ex.GetFullDetails(ExceptionFullDetailsFormat.Xml);
        }
        var newLine = Regex.Escape(Environment.NewLine);
        var match = Regex.Match(fullDetails, $@"^<exception{newLine}  type=""System\.Reflection\.ReflectionTypeLoadException""{newLine}  message=""Exception of type 'System\.Reflection\.ReflectionTypeLoadException' was thrown\.((&#xD;&#xA;|&#xD;|&#xA;)Test Exception)?"">{newLine}  <property{newLine}    name=""TargetSite""{newLine}    value=""Void GetFullDetailsInXmlOfReflectionTypeLoadException\(\)"" />{newLine}  <property{newLine}    name=""HelpLink"" />{newLine}  <property{newLine}    name=""Source""{newLine}    value=""Epiforge\.Extensions\.Components\.Tests"" />{newLine}  <property{newLine}    name=""HResult""{newLine}    value=""-?\d+"" />{newLine}  <stackTrace>{newLine}    <frame{newLine}      type=""Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions""{newLine}      method=""GetFullDetailsInXmlOfReflectionTypeLoadException\(\)""({newLine}      file="".*?\\Epiforge\.Extensions\.Components\.Tests\\ExceptionExtensions\.cs""{newLine}      line=""\d+""{newLine}      column=""\d+"")?{newLine}      offset=""\d+"" />{newLine}  </stackTrace>{newLine}  <loaderException{newLine}    type=""System.Exception""{newLine}    message=""Test Exception"">{newLine}    <property{newLine}      name=""TargetSite""{newLine}      value=""Void GetFullDetailsInXmlOfReflectionTypeLoadException\(\)"" />{newLine}    <property{newLine}      name=""HelpLink"" />{newLine}    <property{newLine}      name=""Source""{newLine}      value=""Epiforge\.Extensions\.Components\.Tests"" />{newLine}    <property{newLine}      name=""HResult""{newLine}      value=""-?\d+"" />{newLine}    <stackTrace>{newLine}      <frame{newLine}        type=""Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions""{newLine}        method=""GetFullDetailsInXmlOfReflectionTypeLoadException\(\)""({newLine}        file="".*?\\Epiforge\.Extensions\.Components\.Tests\\ExceptionExtensions\.cs""{newLine}        line=""\d+""{newLine}        column=""\d+"")?{newLine}        offset=""\d+"" />{newLine}    </stackTrace>{newLine}  </loaderException>{newLine}  <type>Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions</type>{newLine}</exception>$");
        Assert.IsNotNull(match);
        Assert.IsTrue(match.Success);
    }

    [TestMethod]
    public void GetFullDetailsInXmlWithData()
    {
        string fullDetails;
        try
        {
            throw new Exception("Test Exception");
        }
        catch (Exception ex)
        {
            ex.Data["key"] = "value";
            fullDetails = ex.GetFullDetails(ExceptionFullDetailsFormat.Xml);
        }
        var newLine = Regex.Escape(Environment.NewLine);
        var match = Regex.Match(fullDetails, $@"^<exception{newLine}  type=""System\.Exception""{newLine}  message=""Test Exception"">{newLine}  <datum>{newLine}    <key>{newLine}      <string>key</string>{newLine}    </key>{newLine}    <value>{newLine}      <string>value</string>{newLine}    </value>{newLine}  </datum>{newLine}  <property{newLine}    name=""TargetSite""{newLine}    value=""Void GetFullDetailsInXmlWithData\(\)"" />{newLine}  <property{newLine}    name=""HelpLink"" />{newLine}  <property{newLine}    name=""Source""{newLine}    value=""Epiforge\.Extensions\.Components\.Tests"" />{newLine}  <property{newLine}    name=""HResult""{newLine}    value=""-?\d+"" />{newLine}  <stackTrace>{newLine}    <frame{newLine}      type=""Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions""{newLine}      method=""GetFullDetailsInXmlWithData\(\)""({newLine}      file="".*?Epiforge\.Extensions\.Components\.Tests\\ExceptionExtensions.cs""{newLine}      line=""\d+""{newLine}      column=""\d+"")?{newLine}      offset=""\d+"" />{newLine}  </stackTrace>{newLine}</exception>$");
        Assert.IsNotNull(match);
        Assert.IsTrue(match.Success);
    }
}
