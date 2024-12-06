using SixLabors.ImageSharp;

namespace UpscaleAndRotate.Test;

public class UpscaleAndRotateTest
{
	[Theory]
	[InlineData(160, 319, 1, 1, 64, "rotated.gif")]
	public void RotateTest(ushort width, ushort height, byte scaleX, byte scaleY, ushort numFrames, string path)
	{
		byte[] texture = new byte[width * height << 2].DrawBoundingBox(width);
		byte[][] frames = ImageMaker.SameSize(
			frames: [.. Enumerable.Range(0, numFrames)
				.Parallelize(i => (texture
					.Rotate(
						rotatedWidth: out ushort rotatedWidth,
						rotatedHeight: out ushort _,
						radians: Math.Tau * ((double)i / numFrames),
						scaleX: scaleX,
						scaleY: scaleY,
						width: width), rotatedWidth))],
			width: out ushort newWidth,
			height: out _);
		ImageMaker.AnimatedGif(
			width: newWidth,
			frameDelay: 10,
			frames: frames)
			.SaveAsGif(path);
	}
}
