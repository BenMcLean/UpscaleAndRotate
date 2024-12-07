using SixLabors.ImageSharp;

namespace UpscaleAndRotate.Test;

public class UpscaleAndRotateTest
{
	[Theory]
	[InlineData(160, 319, 64, "rotated.gif")]
	[InlineData(160, 319, 64, "rotated-no-box.gif", false)]
	[InlineData(256, 64, 64, "really-wide.gif")]
	[InlineData(256, 64, 64, "really-wide-no-box.gif", false)]
	[InlineData(63, 255, 64, "really-tall.gif")]
	[InlineData(63, 255, 64, "really-tall-no-box.gif", false)]
	[InlineData(63, 63, 64, "square.gif")]
	[InlineData(63, 63, 64, "square-no-box.gif", false)]
	public void GifTest(ushort width, ushort height, ushort numFrames, string path, bool drawBox = true, byte scaleX = 1, byte scaleY = 1, int frameDelay = 10)
	{
		byte[][] frames = ImageMaker.SameSize(
			frames: Frames(
				width: width,
				height: height,
				scaleX: scaleX,
				scaleY: scaleY,
				numFrames: numFrames,
				drawBox: drawBox),
			width: out ushort newWidth,
			height: out _);
		ImageMaker.AnimatedGif(
			width: newWidth,
			frameDelay: frameDelay,
			frames: frames)
			.SaveAsGif(path);
	}
	[Theory]
	[InlineData(160, 319, 5, "rotated{0}.png")]
	[InlineData(160, 319, 5, "rotated-no-box{0}.png", false)]
	public void PngTest(ushort width, ushort height, ushort numFrames, string path, bool drawBox = true, byte scaleX = 1, byte scaleY = 1)
	{
		ushort i = 0;
		foreach ((byte[], ushort) frame in Frames(
			width: width,
			height: height,
			scaleX: scaleX,
			scaleY: scaleY,
			numFrames: numFrames,
			drawBox: drawBox))
			ImageMaker.Png(frame.Item2, frame.Item1).SaveAsPng(string.Format(path, i++));
	}
	public static (byte[], ushort)[] Frames(ushort width, ushort height, byte scaleX, byte scaleY, ushort numFrames, bool drawBox = true)
	{
		byte[] texture = new byte[width * height << 2];
		if (drawBox) texture.DrawBoundingBox(width);
		return [.. Enumerable.Range(0, numFrames)
			.Parallelize(i => (texture.Rotate(
				rotatedWidth: out ushort rotatedWidth,
				rotatedHeight: out ushort _,
				radians: Math.Tau * ((double)i / numFrames),
				scaleX: scaleX,
				scaleY: scaleY,
				width: width), rotatedWidth))];
	}
}
