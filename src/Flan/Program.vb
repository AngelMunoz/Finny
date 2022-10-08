Imports System
Imports System.CommandLine

Imports Flan.Types
Imports Logger = Perla.Logger.Logger

Module CliHandlers
    Friend Async Function HandleAddCommand(package As String, Map As String, Output As String) As Task(Of Integer)
        Dim _map As String = Nothing
        Dim _output As String = Nothing

        If Not Map.ToValueOption().TryGetValue(_map) Then
            _map = "./map.importmap"
        End If

        If Not Output.ToValueOption().TryGetValue(_output) Then
            _output = "./map.importmap"
        End If

        Dim options As New AddPackageOptions With {
            .Package = package,
            .Map = _map,
            .Output = _output
        }
        Try
            Await AddPackage(options)
            Return 0
        Catch ex As Exception
            Logger.log("Unable to add Package", ex)
            Return 1
        End Try
    End Function
End Module

Module CliOptions
    Public ReadOnly Property Package As New [Option](Of String)("--package", "name of the package") With {
        .IsRequired = True
    }
    Public ReadOnly Property Map As New [Option](Of String)("--map", "An optional path to an existing import map") With {
        .IsRequired = False
    }
    Public ReadOnly Property Output As New [Option](Of String)("--output", "Path to where to output the new import map") With {
        .IsRequired = False
    }

    Public ReadOnly Property AddCommand As New Command("add", "adds a new package to the import map") From {
        Package, Map, Output
    }
End Module

Module Program
    Sub Main(args As String())
        AddCommand.SetHandler(AddressOf HandleAddCommand, Package, Map, Output)
        Dim rootCommand As New RootCommand("This is a Package Manager") From {
            AddCommand
        }
        Dim exitCode = rootCommand.InvokeAsync(args)
        Environment.Exit(exitCode.GetAwaiter().GetResult())
    End Sub
End Module
