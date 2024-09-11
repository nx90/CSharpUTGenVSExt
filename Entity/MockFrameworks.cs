using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpUnitTestGeneratorExt.Entity
{
    public class MockFrameworks
    {
        public static readonly string NoneName = "None";
        public static readonly string MoqName = "Moq";
        public static readonly string AutoMoqName = "AutoMoq";
        public static readonly string SimpleStubsName = "SimpleStubs";
        public static readonly string NSubstituteName = "NSubstitute";
        public static readonly string RhinoMocksName = "Rhino Mocks";
        public static readonly string FakeItEasyName = "FakeItEasy";
        public static readonly string JustMockName = "JustMock";

        public static readonly Dictionary<string, MockFramework> mockFrameworks = new Dictionary<string, MockFramework>
        {
            {
                NoneName,
                new MockFramework(
                    MockFrameworks.NoneName,
                    new List<string>(),
                    3,
                    new List<string>(),
                    true,
                    null,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    TestedObjectCreationStyle.TodoStub,
                    null,
                    null,
                    null
                )
            },
            {
                MoqName,
                new MockFramework(
                    MockFrameworks.MoqName,
                    new List<string> { "Moq" },
                    2,
                    new List<string> { "using Moq" },
                    true,
                    "private MockRepository mockRepository;",
                    true,
                    "this.mockRepository = new MockRepository(MockBehavior.Strict);",
                    "private Mock<$InterfaceType$> mock$InterfaceMockName$;",
                    "this.mock$InterfaceMockName$ = this.mockRepository.Create<$InterfaceType$>();",
                    null,
                    null,
                    TestedObjectCreationStyle.HelperMethod,
                    null,
                    "this.mock$InterfaceMockName$.Object",
                    "this.mockRepository.VerifyAll();"
                )
            },
            {
                AutoMoqName,
                new MockFramework(
                    MockFrameworks.AutoMoqName,
                    new List<string> { "AutoMoq" },
                    1,
                    new List<string> { "using AutoMoq", "using Moq" },
                    true,
                    null,
                    false,
                    null,
                    null,
                    null,
                    null,
                    "var mocker = new AutoMoqer();",
                    TestedObjectCreationStyle.DirectCode,
                    "mocker.Create<$ClassName$>();",
                    null,
                    null
                )
            },
            {
                SimpleStubsName,
                new MockFramework(
                    MockFrameworks.SimpleStubsName,
                    new List<string> { "Etg.SimpleStubs", "SimpleStubs" },
                    2,
                    new List<string> { },
                    false,
                    null,
                    true,
                    null,
                    "private Stub$InterfaceName$ stub$InterfaceNameBase$;",
                    "this.stub$InterfaceNameBase$ = new Stub$InterfaceName$();",
                    null,
                    null,
                    TestedObjectCreationStyle.HelperMethod,
                    null,
                    "this.stub$InterfaceNameBase$",
                    null
                )
            },
            {
                NSubstituteName,
                new MockFramework(
                    MockFrameworks.NSubstituteName,
                    new List<string> { "NSubstitute" },
                    0,
                    new List<string> { "using NSubstitute" },
                    true,
                    null,
                    true,
                    null,
                    "private $InterfaceType$ sub$InterfaceMockName$;",
                    "this.sub$InterfaceMockName$ = Substitute.For<$InterfaceType$>();",
                    null,
                    null,
                    TestedObjectCreationStyle.HelperMethod,
                    null,
                    "this.sub$InterfaceMockName$",
                    null
                )
            },
            {
                RhinoMocksName,
                new MockFramework(
                    MockFrameworks.RhinoMocksName,
                    new List<string> { "Rhino.Mocks", "RhinoMocks" },
                    2,
                    new List<string> { "using Rhino.Mocks" },
                    true,
                    null,
                    true,
                    null,
                    "private $InterfaceType$ stub$InterfaceMockName$;",
                    "this.stub$InterfaceMockName$ = MockRepository.GenerateStub<$InterfaceType$>();",
                    null,
                    null,
                    TestedObjectCreationStyle.HelperMethod,
                    null,
                    "this.stub$InterfaceMockName$",
                    null
                )
            },
            {
                FakeItEasyName,
                new MockFramework(
                    MockFrameworks.FakeItEasyName,
                    new List<string> { "FakeItEasy" },
                    2,
                    new List<string> { "using FakeItEasy" },
                    true,
                    null,
                    true,
                    null,
                    "private $InterfaceType$ fake$InterfaceMockName$;",
                    "this.fake$InterfaceMockName$ = A.Fake<$InterfaceType$>();",
                    null,
                    null,
                    TestedObjectCreationStyle.HelperMethod,
                    null,
                    "this.fake$InterfaceMockName$",
                    null
                )
            },
            {
                JustMockName,
                new MockFramework(
                    MockFrameworks.JustMockName,
                    new List<string> { "JustMock", "Telerik.JustMock" },
                    2,
                    new List<string> { "using Telerik.JustMock" },
                    true,
                    null,
                    true,
                    null,
                    "private $InterfaceType$ mock$InterfaceMockName$;",
                    "this.mock$InterfaceMockName$ = Mock.Create<$InterfaceType$>();",
                    null,
                    null,
                    TestedObjectCreationStyle.HelperMethod,
                    null,
                    "this.mock$InterfaceMockName$",
                    null
                )
            }
        };

        public static MockFramework Default = mockFrameworks[MoqName];

        public static MockFramework Get(string name)
        {
            if (mockFrameworks.TryGetValue(name, out var mockFramework))
            {
                return mockFramework;
            }
            return Default;
        }
    }
}
