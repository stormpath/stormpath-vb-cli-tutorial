' <copyright file="Demo.vb" company="Stormpath, Inc.">
' Copyright (c) 2015 Stormpath, Inc.
'
' Licensed under the Apache License, Version 2.0 (the "License");
' you may not use this file except in compliance with the License.
' You may obtain a copy of the License at
'
'      http://www.apache.org/licenses/LICENSE-2.0
'
' Unless required by applicable law or agreed to in writing, software
' distributed under the License is distributed on an "AS IS" BASIS,
' WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
' See the License for the specific language governing permissions and
' limitations under the License.
' </copyright>

Option Strict On
Option Explicit On
Option Infer On
Imports Stormpath.SDK
Imports Stormpath.SDK.Account
Imports Stormpath.SDK.Api
Imports Stormpath.SDK.Client
Imports Stormpath.SDK.Error
Imports Stormpath.SDK.Group

Module Demo

    Sub Main()
        RunDemo.GetAwaiter.GetResult()
    End Sub

    Private Async Function RunDemo() As Task

        ' Load an API Key and Secret from the specified file path
        ' This is only necessary if the API Key is not stored in environment variables
        ' or in the default location (~\.stormpath\apiKey.properties).
        Dim apiKey = ClientApiKeys.Builder _
            .SetFileLocation("~\.stormpath\apiKey.properties") _
            .Build()

        ' Build a client object - everything starts here!
        ' .SetApiKey() is only necessary if specifying an API Key location above.
        Dim client = Clients.Builder _
            .SetApiKey(apiKey) _
            .Build()

        ' Get the default "My Application" application
        Dim app = Await client.GetApplications _
            .Where(Function(a) a.Name = "My Application") _
            .FirstAsync()
        Console.WriteLine("Connected to Stormpath")

        ' Create a user who can log into the application
        Dim joe = client.Instantiate(Of IAccount) _
            .SetGivenName("Joe") _
            .SetSurname("Stormtrooper") _
            .SetEmail("tk421@deathstar.co") _
            .SetPassword("Changeme!123")
        joe.CustomData.Put(New With {.read = True, .write = False})

        Await app.CreateAccountAsync(joe)
        Console.WriteLine("Created account " & joe.Email)

        ' Try logging in Joe
        Try
            Dim loginResult = Await app.AuthenticateAccountAsync("tk421@deathstar.co", "Changeme!123")
            Dim loginAccount = Await loginResult.GetAccountAsync()
            Console.WriteLine("User " & loginAccount.FullName & " logged in!")
        Catch rex As ResourceException
            Console.WriteLine("Could not log in. Error: " & rex.Message)
        End Try

        ' Create a demo group for Joe to be part of
        ' And an admin group Joe is NOT part of
        Dim demoUsers = client.Instantiate(Of IGroup) _
            .SetName("DemoUsers") _
            .SetDescription("Demo users who do not have administrator access.")
        Dim demoAdmins = client.Instantiate(Of IGroup) _
            .SetName("DemoAdmins") _
            .SetDescription("Demo users who have administrator access.")

        Await Task.WhenAll(
            app.CreateGroupAsync(demoUsers),
            app.CreateGroupAsync(demoAdmins))

        ' Add Joe to the Users group
        Await joe.AddGroupAsync(demoUsers)

        ' Get role-based authorization from group
        Dim roles = (Await joe.GetGroups().ToListAsync()) _
            .Select(Function(g) g.Name)
        Console.WriteLine("Roles for " & joe.GivenName & ": " &
                          String.Join(", ", roles))

        ' Get fine-grained permissions from customData
        Dim joeCustomData = Await joe.GetCustomDataAsync()
        Dim canRead = CBool(joeCustomData("read"))
        Dim canWrite = CBool(joeCustomData("write"))
        Console.WriteLine("Can Joe read? " & canRead)
        Console.WriteLine("Can Joe write? " & canWrite)

        ' Reset Joe's password. This initiates a password reset workflow:
        ' An email is sent to Joe, which includes a callback link to your
        ' application, and a token in the URL queryString.
        Dim token = Await app.SendPasswordResetEmailAsync("tk421@deathstar.co")
        ' Once you have the token, the workflow can be completed.
        Await app.ResetPasswordAsync(token.GetValue(), "ItsATrap1138!")
        Console.WriteLine("Password reset for " & joe.Email)

        ' Clean up
        Await Task.WhenAll(
            demoUsers.DeleteAsync(),
            demoAdmins.DeleteAsync())
        Await joe.DeleteAsync()
        Console.WriteLine("Cleaned up API objects")

        ' Wait for user input before closing console window
        Console.ReadKey(False)
    End Function

End Module