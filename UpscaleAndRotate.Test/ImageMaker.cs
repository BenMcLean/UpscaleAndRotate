using SixLabors.ImageSharp;
using System.Buffers.Binary;

namespace UpscaleAndRotate.Test;

public static class ImageMaker
{
	public const int DefaultFrameDelay = 100;
	#region ImageSharp
	public static Image<SixLabors.ImageSharp.PixelFormats.Rgba32> Png(ushort width = 0, params byte[] bytes)
	{
		if (width < 1) width = (ushort)Math.Sqrt(bytes.Length >> 2);
		return Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(
			data: bytes,
			width: width,
			height: (bytes.Length >> 2) / width);
	}
	public static Image<SixLabors.ImageSharp.PixelFormats.Rgba32> AnimatedGif(ushort width = 0, int frameDelay = DefaultFrameDelay, ushort repeatCount = 0, params byte[][] frames)
	{
		if (width < 1) width = (ushort)Math.Sqrt(frames[0].Length >> 2);
		int height = (frames[0].Length >> 2) / width;
		Image<SixLabors.ImageSharp.PixelFormats.Rgba32> gif = new(width, height);
		SixLabors.ImageSharp.Formats.Gif.GifMetadata gifMetaData = gif.Metadata.GetGifMetadata();
		gifMetaData.RepeatCount = repeatCount;
		gifMetaData.ColorTableMode = SixLabors.ImageSharp.Formats.Gif.GifColorTableMode.Local;
		foreach (byte[] frame in frames)
		{
			Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image = Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(
				data: frame,
				width: width,
				height: height);
			SixLabors.ImageSharp.Formats.Gif.GifFrameMetadata metadata = image.Frames.RootFrame.Metadata.GetGifMetadata();
			metadata.FrameDelay = frameDelay;
			metadata.DisposalMethod = SixLabors.ImageSharp.Formats.Gif.GifDisposalMethod.RestoreToBackground;
			gif.Frames.AddFrame(image.Frames.RootFrame);
		}
		gif.Frames.RemoveFrame(0);//I don't know why ImageSharp has me doing this but if I don't then I get an extra transparent frame at the start.
		return gif;
	}
	#endregion ImageSharp
	#region PLINQ
	/// <summary>
	/// Parallelizes the execution of a Select query while preserving the order of the source sequence.
	/// </summary>
	public static List<TResult> Parallelize<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector) => [.. source
		.Select((element, index) => (element, index))
		.AsParallel()
		.Select(sourceTuple => (result: selector(sourceTuple.element), sourceTuple.index))
		.OrderBy(resultTuple => resultTuple.index)
		.AsEnumerable()
		.Select(resultTuple => resultTuple.result)];
	#endregion PLINQ
	#region PixelDraw
	/// <summary>
	/// Draws a rectangle of the specified color
	/// </summary>
	/// <param name="texture">raw rgba8888 pixel data to be modified</param>
	/// <param name="color">rgba color to draw</param>
	/// <param name="x">upper left corner of rectangle</param>
	/// <param name="y">upper left corner of rectangle</param>
	/// <param name="width">width of texture or 0 to assume square texture</param>
	/// <returns>same texture with rectangle drawn</returns>
	public static byte[] DrawRectangle(this byte[] texture, uint color, int x, int y, int rectWidth = 1, int rectHeight = 1, ushort width = 0)
	{
		if (rectWidth == 1 && rectHeight == 1)
			return texture.DrawPixel((ushort)x, (ushort)y, color, width);
		if (rectHeight < 1) rectHeight = rectWidth;
		if (x < 0)
		{
			rectWidth += x;
			x = 0;
		}
		if (y < 0)
		{
			rectHeight += y;
			y = 0;
		}
		if (width < 1) width = (ushort)Math.Sqrt(texture.Length >> 2);
		int height = texture.Length / width >> 2;
		if (rectWidth < 1 || rectHeight < 1 || x >= width || y >= height) return texture;
		rectWidth = Math.Min(rectWidth, width - x);
		rectHeight = Math.Min(rectHeight, height - y);
		int xSide = width << 2,
			x4 = x << 2,
			offset = y * xSide + x4,
			rectWidth4 = rectWidth << 2,
			yStop = offset + xSide * rectHeight;
		for (int x2 = offset; x2 < offset + rectWidth4; x2 += 4)
			BinaryPrimitives.WriteUInt32BigEndian(
				destination: texture.AsSpan(
					start: x2,
					length: 4),
				value: color);
		for (int y2 = offset + xSide; y2 < yStop; y2 += xSide)
			Array.Copy(
				sourceArray: texture,
				sourceIndex: offset,
				destinationArray: texture,
				destinationIndex: y2,
				length: rectWidth4);
		return texture;
	}
	public static byte[] DrawBoundingBox(this byte[] texture, ushort width = 0)
	{
		if (width < 1) width = (ushort)Math.Sqrt(texture.Length >> 2);
		ushort height = UpscaleAndRotate.Height(length: texture.Length, width: width);
		return texture.DrawRectangle(
				x: 0,
				y: 0,
				color: UpscaleAndRotate.Yellow,
				rectWidth: (ushort)(width - 1),
				width: width)
			.DrawRectangle(
				x: 1,
				y: (ushort)(height - 1),
				color: UpscaleAndRotate.Red,
				rectWidth: (ushort)(width - 1),
				width: width)
			.DrawRectangle(
				x: 0,
				y: 1,
				color: UpscaleAndRotate.Blue,
				rectHeight: (ushort)(height - 1),
				width: width)
			.DrawRectangle(
				x: (ushort)(width - 1),
				y: 0,
				color: UpscaleAndRotate.Green,
				rectHeight: (ushort)(height - 1),
				width: width);
	}
	/// <summary>
	/// Makes a new texture and copies the old texture to its upper left corner
	/// </summary>
	/// <param name="texture">raw rgba8888 pixel data of source image</param>
	/// <param name="newWidth">width of newly resized texture</param>
	/// <param name="newHeight">height of newly resized texture</param>
	/// <param name="width">width of texture or 0 to assume square texture</param>
	/// <returns>new raw rgba8888 pixel data of width newWidth</returns>
	public static byte[] Resize(this byte[] texture, ushort newWidth, ushort newHeight, ushort width = 0)
	{
		if (newWidth < 1) throw new ArgumentOutOfRangeException("newWidth cannot be smaller than 1. Was: \"" + newWidth + "\"");
		if (newHeight < 1) throw new ArgumentOutOfRangeException("newHeight cannot be smaller than 1. Was: \"" + newHeight + "\"");
		newWidth <<= 2; // newWidth *= 4;
		int xSide = (width < 1 ? (int)Math.Sqrt(texture.Length >> 2) : width) << 2;
		byte[] resized = new byte[newWidth * newHeight];
		if (newWidth == xSide)
			Array.Copy(
				sourceArray: texture,
				destinationArray: resized,
				length: Math.Min(texture.Length, resized.Length));
		else
		{
			int newXside = Math.Min(xSide, newWidth);
			for (int y1 = 0, y2 = 0; y1 < texture.Length && y2 < resized.Length; y1 += xSide, y2 += newWidth)
				Array.Copy(
					sourceArray: texture,
					sourceIndex: y1,
					destinationArray: resized,
					destinationIndex: y2,
					length: newXside);
		}
		return resized;
	}
	public static byte[][] SameSize(this (byte[], ushort)[] frames, out ushort width, out ushort height)
	{
		width = frames.Max(frame => frame.Item2);
		height = frames.Max(frame => UpscaleAndRotate.Height(frame.Item1.Length, frame.Item2));
		ushort newWidth = width, newHeight = height;
		return [.. frames.Parallelize(frame => frame.Item1.Resize(
			newWidth: newWidth,
			newHeight: newHeight,
			width: frame.Item2))];
	}
	#endregion PixelDraw
}
