Imports System
Imports System.CommandLine
Imports System.IO
Imports Perla.Commands

Module FlanCommands

    Public Function GetInit() As Command
        Dim command = New Command("init", $"Creates a '{Perla.Constants.PerlaConfigName}' File that will be used to handle your import map")
        command.SetHandler(AddressOf Actions.CreatePerlaFile)
        Return command
    End Function

    Public Function GetInsertInFile() As Command
        Dim command = New Command("set", "Adds or updates the cont")
        Dim layoutFile = New Argument(Of FileInfo)("layout", "Path to the layout file where you'd like to add/update the import map") With {
            .Arity = ArgumentArity.ExactlyOne
        }
        command.AddArgument(layoutFile)
        command.SetHandler(New Action(Of FileInfo)(AddressOf Actions.AddToLayout), layoutFile)

        Return command
    End Function

End Module


Module Program
    Sub Main(args As String())
        Dim rootCommand As New RootCommand("Import Maps Manager")

        rootCommand.AddCommand(Commands.AddPackage)
        rootCommand.AddCommand(Commands.AddResolution)
        rootCommand.AddCommand(Commands.RemovePackage)
        rootCommand.AddCommand(Commands.RestoreImportMap)
        rootCommand.AddCommand(Commands.ListPackages)
        rootCommand.AddCommand(FlanCommands.GetInit())
        rootCommand.AddCommand(FlanCommands.GetInsertInFile())

        Dim exitCode = rootCommand.InvokeAsync(args)
        Environment.Exit(exitCode.GetAwaiter().GetResult())
    End Sub
End Module
