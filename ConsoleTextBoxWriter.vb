Imports System.IO
Imports System.Text
Imports System.Windows.Threading

Public Class ConsoleTextBoxWriter
    Inherits TextWriter

    Private textBox As TextBox
    Private dispatcher As Dispatcher

    Public Sub New(ByVal textBox As TextBox, ByRef dispatcher As Dispatcher)
        Console.SetOut(Me)
        Me.textBox = textBox
        Me.dispatcher = dispatcher
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
        dispatcher.Invoke(Sub()
                              textBox.AppendText(value)
                              textBox.ScrollToEnd()
                          End Sub)
    End Sub
End Class
