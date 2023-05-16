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
        var match = Regex.Match(fullDetails, $@"^-- Exception: Test Exception{newLine}   at void Epiforge\.Extensions\.Components\.Tests\.ExceptionExtensions\.GetFullDetailsInPlainText\(\)( in .*?Epiforge\.Extensions\.Components\.Tests/ExceptionExtensions\.cs:line \d+)?$");
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
