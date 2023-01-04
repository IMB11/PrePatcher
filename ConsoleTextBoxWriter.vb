Imports System.IO
Imports System.Text

Public Class ConsoleTextBoxWriter
    Inherits TextWriter

    Private textBox As TextBox

    Public Sub New(ByVal textBox As TextBox)
        Console.SetOut(Me)
        Me.textBox = textBox
    End Sub

    Public Overrides ReadOnly Property Encoding As Encoding
        Get
            Return Encoding.UTF8
        End Get
    End Property

    Public Overrides Sub Write(ByVal value As String)
        WriteImp(value)
    End Sub

    Public Overrides Sub WriteLine(ByVal value As String)
        WriteImp(value & Environment.NewLine)
    End Sub

    Private Sub WriteImp(ByVal value As String)
        textBox.AppendText(value)
    End Sub
End Class
