using HololensAirplaneViewer.Services;
using Xunit;

namespace HololensAirplaneViewer.Tests
{
    public class BitmapGlyphFontTests
    {
        [Fact]
        public void SupportedGlyphsUseSevenByNineMasks()
        {
            foreach (char character in BitmapGlyphFont.SupportedCharacters)
            {
                var rows = BitmapGlyphFont.GetRows(character);

                Assert.Equal(9, rows.Count);
                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    string row = rows[rowIndex];
                    Assert.Equal(7, row.Length);
                    Assert.All(row, pixel => Assert.True(pixel == '#' || pixel == '.'));
                }
            }
        }

        [Fact]
        public void LettersDigitsAndPunctuationContainVisiblePixels()
        {
            const string visibleCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.,:";

            foreach (char character in visibleCharacters)
            {
                Assert.Contains("#", string.Concat(BitmapGlyphFont.GetRows(character)));
            }
        }

        [Fact]
        public void LowercaseCharactersUseUppercaseGlyphs()
        {
            foreach (char character in "abcdefghijklmnopqrstuvwxyz")
            {
                Assert.Equal(BitmapGlyphFont.GetRows(char.ToUpperInvariant(character)),
                    BitmapGlyphFont.GetRows(character));
            }
        }

        [Fact]
        public void UnsupportedCharactersUseBlankGlyph()
        {
            var rows = BitmapGlyphFont.GetRows('?');

            Assert.Equal(9, rows.Count);
            Assert.DoesNotContain("#", string.Concat(rows));
        }

        [Fact]
        public void SevenByNineHasOverFourTimesTheSamplesOfThreeByFive()
        {
            Assert.Equal(63, BitmapGlyphFont.GlyphWidth * BitmapGlyphFont.GlyphHeight);
            Assert.Equal(15, 3 * 5);
            Assert.True(BitmapGlyphFont.GlyphWidth * BitmapGlyphFont.GlyphHeight >= 4 * 3 * 5);
        }

        [Fact]
        public void SevenByNineGlyphFitsInsideAtlasCell()
        {
            Assert.True(
                BitmapGlyphFont.AtlasPaddingLeft + BitmapGlyphFont.GlyphWidth * BitmapGlyphFont.AtlasPixelScale
                <= BitmapGlyphFont.AtlasCellWidth);
            Assert.True(
                BitmapGlyphFont.AtlasPaddingTop + BitmapGlyphFont.GlyphHeight * BitmapGlyphFont.AtlasPixelScale
                <= BitmapGlyphFont.AtlasCellHeight);
        }
    }
}
