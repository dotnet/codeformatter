// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.DotNet.CodeFormatting.Tests
{
	public class RemoveRegionsTests : SyntaxRuleTestBase
	{
		internal override ISyntaxFormattingRule Rule
		{
			get
			{
				return new Rules.RemoveRegionsRule();
			}
		}

		[Fact]
		public void TestRemoveRegions()
		{
			var text = @"
#region Region 1

//comment
#endregion Region 1

class WithRegions
{
	#region have region here
	public static void DoNothing()
	{
		#region inside method

		#endregion inside method
	}
#endregion   
}
";
			var expected = @"

//comment

class WithRegions
{
    public static void DoNothing()
    {

    }
}
";
			Verify(text, expected);
		}
	}
}
