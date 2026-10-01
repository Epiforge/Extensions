var lines = Probe.Run(args.Length > 1 ? args[1] : "console");
foreach (var line in lines)
    Console.WriteLine(line);
if (args.Length > 0)
    File.WriteAllLines(args[0], lines);
return lines.Any(line => line.StartsWith("FAIL ", StringComparison.Ordinal)) ? 1 : 0;
