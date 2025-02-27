﻿' Copyright (C) 2023  Andy https://github.com/AAndyProgram
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY
Imports System.Threading
Imports SCrawler.API
Imports SCrawler.API.Base
Friend Class ListImagesLoader
    Private ReadOnly Property MyList As ListView
    Private Structure UserOption : Implements IComparable(Of UserOption)
        Friend ReadOnly User As IUserData
        Friend ReadOnly LVI As ListViewItem
        Friend Index As Integer
        Friend ReadOnly Property Key As String
            Get
                Return LVI.Name
            End Get
        End Property
        Friend Sub New(ByVal u As IUserData, ByVal l As ListView)
            User = u
            LVI = u.GetLVI(l)
            Index = u.Index
        End Sub
        Friend Function CompareTo(ByVal Other As UserOption) As Integer Implements IComparable(Of UserOption).CompareTo
            Return Index.CompareTo(Other.Index)
        End Function
    End Structure
    Friend Sub New(ByRef l As ListView)
        MyList = l
    End Sub
    Private UserDataList As List(Of UserOption)
    Private UpdateInProgress As Boolean = False
    Private ImageThread As Thread
    Private Sub UpdateImages()
        If UserDataList.ListExists And Not If(ImageThread?.IsAlive, False) Then
            ImageThread = New Thread(New ThreadStart(Sub()
                                                         Dim ar As IAsyncResult = Nothing
                                                         Dim a As Action = Sub()
                                                                               Try
                                                                                   If UserDataList.ListExists Then
                                                                                       For i% = 0 To UserDataList.Count - 1
                                                                                           With UserDataList(i).User
                                                                                               Select Case Settings.ViewMode.Value
                                                                                                   Case View.LargeIcon : MyList.LargeImageList.Images.Add(.Key, .GetPicture())
                                                                                                   Case View.SmallIcon : MyList.SmallImageList.Images.Add(.Key, .GetPicture())
                                                                                               End Select
                                                                                           End With
                                                                                           Application.DoEvents()
                                                                                       Next
                                                                                       UserDataList.Clear()
                                                                                       GC.Collect()
                                                                                   End If
                                                                               Catch iex As ArgumentOutOfRangeException
                                                                               Catch ex As Exception
                                                                                   ErrorsDescriber.Execute(EDP.SendToLog, ex, "[ListImagesLoader.UpdateImages]")
                                                                               End Try
                                                                               If Not ar Is Nothing Then MyList.EndInvoke(ar)
                                                                               UpdateInProgress = False
                                                                           End Sub
                                                         If MyList.InvokeRequired Then
                                                             ar = MyList.BeginInvoke(a)
                                                         Else
                                                             a.Invoke
                                                         End If
                                                     End Sub)) With {.IsBackground = True}
            ImageThread.SetApartmentState(ApartmentState.MTA)
            ImageThread.Start()
        End If
    End Sub
    Private Sub InterruptUpdate()
        Try
            If UserDataList.ListExists Then UserDataList.Clear() : Application.DoEvents()
            If If(ImageThread?.IsAlive, False) Then ImageThread.Abort() : Application.DoEvents()
        Catch ex As Exception
            ErrorsDescriber.Execute(EDP.SendToLog, ex, "[ListImagesLoader.InterruptUpdate]")
        End Try
    End Sub
    Friend Sub Update()
        Try
            If UpdateInProgress Then InterruptUpdate()
            If Not UpdateInProgress Then
                UpdateInProgress = True
                Dim a As Action = Sub()
                                      With MyList
                                          .Items.Clear()
                                          If Not .LargeImageList Is Nothing Then .LargeImageList.Images.Clear()
                                          .LargeImageList = New ImageList
                                          If Not .SmallImageList Is Nothing Then .SmallImageList.Images.Clear()
                                          .SmallImageList = New ImageList
                                          If Settings.ViewModeIsPicture Then
                                              .LargeImageList.ColorDepth = ColorDepth.Depth32Bit
                                              .SmallImageList.ColorDepth = ColorDepth.Depth32Bit
                                              .LargeImageList.ImageSize = New Size(DivideWithZeroChecking(Settings.MaxLargeImageHeight.Value, 100) * 75, Settings.MaxLargeImageHeight.Value)
                                              .SmallImageList.ImageSize = New Size(DivideWithZeroChecking(Settings.MaxSmallImageHeight.Value, 100) * 75, Settings.MaxSmallImageHeight.Value)
                                          End If
                                      End With
                                  End Sub
                If MyList.InvokeRequired Then MyList.Invoke(a) Else a.Invoke
                If Settings.Users.Count > 0 Then
                    Settings.Users.Sort()
                    Dim v As View = Settings.ViewMode.Value

                    With MyList
                        MyList.BeginUpdate()

                        If Settings.FastProfilesLoading Then
                            Settings.Users.ListReindex

                            UserDataList = (From u As IUserData In Settings.Users Where u.FitToAddParams Select New UserOption(u, MyList)).ListIfNothing
                            If UserDataList.ListExists Then UserDataList.Sort()

                            If UserDataList.ListExists Then
                                .Items.AddRange(UserDataList.Select(Function(u) u.LVI).ToArray)
                                If Settings.ViewModeIsPicture Then
                                    MyList.EndUpdate()
                                    UpdateImages()
                                Else
                                    UserDataList.Clear()
                                    UpdateInProgress = False
                                End If
                            Else
                                UpdateInProgress = False
                            End If
                        Else
                            Dim t As New List(Of Task)
                            For Each User As IUserData In Settings.Users
                                If User.FitToAddParams Then
                                    If Settings.ViewModeIsPicture Then
                                        t.Add(Task.Run(Sub() UpdateUser(User, True)))
                                    Else
                                        UpdateUser(User, True)
                                    End If
                                End If
                            Next
                            If t.Count > 0 Then Task.WhenAll(t.ToArray) : t.Clear()
                            UpdateInProgress = False
                        End If
                    End With
                    MyList.EndUpdate()
                Else
                    UpdateInProgress = False
                End If
            Else
                MsgBoxE({"User list update aborted. Click the 'Refresh' button to refresh the user list.", "Update user list"}, vbExclamation)
            End If
        Catch ex As Exception
            ErrorsDescriber.Execute(EDP.SendToLog, ex, "[ListImagesLoader.Update]")
        End Try
    End Sub
    Friend Sub UpdateUser(ByVal User As IUserData, ByVal Add As Boolean)
        Try
            Dim a As Action
            If Add Then
                a = Sub()
                        With MyList
                            Select Case Settings.ViewMode.Value
                                Case View.LargeIcon : .LargeImageList.Images.Add(User.Key, User.GetPicture())
                                Case View.SmallIcon : .SmallImageList.Images.Add(User.Key, User.GetPicture())
                            End Select
                            .Items.Add(User.GetLVI(MyList))
                        End With
                    End Sub
            Else
                a = Sub()
                        With MyList
                            Dim i% = .Items.IndexOfKey(User.Key)
                            Dim ImgIndx%
                            If i >= 0 Then
                                Select Case Settings.ViewMode.Value
                                    Case View.LargeIcon
                                        ImgIndx = .LargeImageList.Images.IndexOfKey(User.Key)
                                        If ImgIndx >= 0 Then .LargeImageList.Images(ImgIndx) = User.GetPicture()
                                    Case View.SmallIcon
                                        ImgIndx = .SmallImageList.Images.IndexOfKey(User.Key)
                                        If ImgIndx >= 0 Then .SmallImageList.Images(ImgIndx) = User.GetPicture()
                                End Select
                                With .Items(i) : .Text = User.ToString() : .Group = User.GetLVIGroup(MyList) : End With
                                ApplyLVIColor(User, .Items(i), False)
                            End If
                        End With
                    End Sub
            End If
            If MyList.InvokeRequired Then MyList.Invoke(a) Else a.Invoke
        Catch ex As Exception
        End Try
    End Sub
    Friend Shared Function ApplyLVIColor(ByVal User As IUserData, ByVal LVI As ListViewItem, ByVal IsInit As Boolean) As ListViewItem
        With LVI
            If Not User.Exists Then
                .BackColor = MyColor.DeleteBack
                .ForeColor = MyColor.DeleteFore
            ElseIf User.Suspended Then
                .BackColor = MyColor.EditBack
                .ForeColor = MyColor.EditFore
            ElseIf CheckUserCollection(User) Then
                .BackColor = Color.LightSkyBlue
                .ForeColor = Color.MidnightBlue
            Else
                .BackColor = Settings.UserListBackColorF
                .ForeColor = Settings.UserListForeColorF
            End If
        End With
        Return LVI
    End Function
    Private Shared Function CheckUserCollection(ByVal User As IUserData) As Boolean
        If User.IsCollection Then
            With DirectCast(User, UserDataBind)
                If .Count > 0 Then Return .Collections.Exists(Function(c) Not c.Exists) Else Return False
            End With
        Else
            Return False
        End If
    End Function
End Class