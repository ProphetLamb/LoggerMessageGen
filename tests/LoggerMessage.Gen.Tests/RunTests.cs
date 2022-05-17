using System;

using FluentAssertions;

using LoggerMessage.Gen;

using Microsoft.Extensions.Logging;

using NUnit.Framework;

namespace LoggerMessage.Gen.Tests;

[TestFixture]
public class RunTests
{

}

[LoggerMessage("LoginFailed", LogLevel.Warning, "Login failed. User = {User:string}, Host = {Host:string}, Attempt = {Attempt:int}.")]
internal class Constroller
{
}
