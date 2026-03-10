namespace Element.CloudDistributedLock.Tests
{
    public class SessionTokenParserTests
    {
        [Fact]
        public void Parse_SimpleSessionToken_ReturnsGlobalLSN()
        {
            // Simple format: {pkrangeid}:{globalLSN}
            var result = SessionTokenParser.Parse("0:100");
            Assert.Equal(100, result);
        }

        [Fact]
        public void Parse_VectorSessionToken_ReturnsGlobalLSN()
        {
            // Vector format: {pkrangeid}:{Version}#{GlobalLSN}#{RegionId1}={LocalLsn1}
            var result = SessionTokenParser.Parse("0:1#200#1=200");
            Assert.Equal(200, result);
        }

        [Fact]
        public void Parse_VectorSessionTokenWithMultipleRegions_ReturnsGlobalLSN()
        {
            // Vector format with multiple regions
            var result = SessionTokenParser.Parse("0:1#500#1=400#2=350#3=500");
            Assert.Equal(500, result);
        }

        [Fact]
        public void Parse_LargeGlobalLSN_ReturnsCorrectValue()
        {
            var result = SessionTokenParser.Parse("0:1#9999999999#1=9999999999");
            Assert.Equal(9999999999L, result);
        }

        [Fact]
        public void Parse_DifferentPkRangeId_ReturnsGlobalLSN()
        {
            var result = SessionTokenParser.Parse("5:1#300#1=300");
            Assert.Equal(300, result);
        }

        [Fact]
        public void Parse_SimpleTokenWithZeroLSN_ReturnsZero()
        {
            var result = SessionTokenParser.Parse("0:0");
            Assert.Equal(0, result);
        }

        [Fact]
        public void Parse_VectorTokenWithZeroGlobalLSN_ReturnsZero()
        {
            var result = SessionTokenParser.Parse("0:1#0#1=0");
            Assert.Equal(0, result);
        }
    }
}
