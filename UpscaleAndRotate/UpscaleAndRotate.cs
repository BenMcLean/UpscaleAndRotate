using System;
using System.Buffers.Binary;

namespace UpscaleAndRotate;

public static class UpscaleAndRotate
{
	/// <summary>
	/// Rotates a texture after optionally upscaling in one operation so that the resulting upscaled texture is aligned so that its leftmost pixel is on x=0 and its highest pixel is on y=0.
	/// </summary>
	public static byte[] Rotate(this byte[] texture, out ushort rotatedWidth, out ushort rotatedHeight, double radians = 0d, byte scaleX = 1, byte scaleY = 1, ushort width = 0)
	{
		if (scaleX < 1) throw new ArgumentOutOfRangeException(nameof(scaleX));
		if (scaleY < 1) throw new ArgumentOutOfRangeException(nameof(scaleY));
		if (width < 1) width = (ushort)Math.Sqrt(texture.Length >> 2);
		if (width > ushort.MaxValue / scaleX)
			throw new OverflowException("Scaled width exceeds maximum allowed size.");
		ushort height = Height(texture.Length, width);
		if (height > ushort.MaxValue / scaleY)
			throw new OverflowException("Scaled height exceeds maximum allowed size.");
		ushort scaledWidth = (ushort)(width * scaleX),
			scaledHeight = (ushort)(height * scaleY);
		radians %= Tau;
		double cos = Math.Cos(radians),
			sin = Math.Sin(radians),
			absCos = Math.Abs(cos),
			absSin = Math.Abs(sin);
		uint rWidth = (uint)(scaledWidth * absCos + scaledHeight * absSin),
			rHeight = (uint)(scaledWidth * absSin + scaledHeight * absCos);
		if (rWidth > ushort.MaxValue || rHeight > ushort.MaxValue)
			throw new OverflowException("Rotated dimensions exceed maximum allowed size.");
		if (rWidth * rHeight > int.MaxValue >> 2)
			throw new OverflowException("Resulting image would be too large to allocate");
		rotatedWidth = (ushort)rWidth;
		rotatedHeight = (ushort)rHeight;
		ushort halfRotatedWidth = (ushort)(rotatedWidth >> 1),
			halfRotatedHeight = (ushort)(rotatedHeight >> 1);
		double offsetX = (scaledWidth >> 1) - cos * halfRotatedWidth - sin * halfRotatedHeight,
			offsetY = (scaledHeight >> 1) - cos * halfRotatedHeight + sin * halfRotatedWidth;
		//double[][] corners = [
		//	[-halfRotatedWidth + offsetX, -halfRotatedHeight + offsetY],//top-left
		//	[halfRotatedWidth + offsetX, -halfRotatedHeight + offsetY],//top-right
		//	[halfRotatedWidth + offsetX, halfRotatedHeight + offsetY],//bottom-right
		//	[-halfRotatedWidth + offsetX, halfRotatedHeight + offsetY],//bottom-left
		//];
		byte[] rotated = new byte[rotatedWidth * rotatedHeight << 2];
		bool isNearZero = absCos < 1e-10 || absSin < 1e-10;
		for (ushort y = 0; y < rotatedHeight; y++)
		{
			ushort startX = 0, endX = (ushort)(rotatedWidth - 1);
			if (!isNearZero)
			{
				//TODO calculate startX and endX to avoid iterating outsize the bounding box
			}
			for (ushort x = startX; x < endX; x++)
			{
				double sourceX = (x * cos + y * sin + offsetX) / scaleX,
					sourceY = (y * cos - x * sin + offsetY) / scaleY;
				if (sourceX >= 0d && sourceX < width && sourceY >= 0d && sourceY < height)
					rotated.DrawPixel(
						x: x,
						y: y,
						color: texture.Pixel(
							x: (ushort)Math.Floor(sourceX),
							y: (ushort)Math.Floor(sourceY),
							width: width),
						width: rotatedWidth);
			}
#if DEBUG
			rotated.DrawPixel(
				x: startX,
				y: y,
				color: Purple,
				width: rotatedWidth);
			rotated.DrawPixel(
				x: endX,
				y: y,
				color: Orange,
				width: rotatedWidth);
#endif
		}
		return rotated;
	}
	#region Utilities
	public const double Tau = 2d * Math.PI;
	public const uint Red = 0xFF0000FFu,
		Yellow = 0xFFFF00FFu,
		Black = 0x000000FFu,
		White = 0xFFFFFFFFu,
		Green = 0x00FF00FFu,
		Blue = 0x0000FFFFu,
		Orange = 0xFFA500FFu,
		Indigo = 0x4B0082FFu,
		Violet = 0x8F00FFFFu,
		Purple = 0xFF00FFFFu;
	public static ushort Height(int length, ushort width = 0) =>
		width > 0 ?
			(ushort)(length / width >> 2)
			: (ushort)Math.Sqrt(length >> 2);
	public static uint Pixel(this byte[] texture, ushort x, ushort y, ushort width = 0) => BinaryPrimitives.ReadUInt32BigEndian(texture.AsSpan(
		start: y * ((width < 1 ? (int)Math.Sqrt(texture.Length >> 2) : width) << 2) + (x << 2),
		length: 4));
	/// <summary>
	/// Draws one pixel of the specified color
	/// </summary>
	/// <param name="texture">raw rgba8888 pixel data</param>
	/// <param name="color">rgba color to draw</param>
	/// <param name="width">width of texture or 0 to assume square texture</param>
	/// <returns>same texture with pixel drawn</returns>
	public static byte[] DrawPixel(this byte[] texture, ushort x, ushort y, uint color = White, ushort width = 0)
	{
		ushort xSide = (ushort)((width < 1 ? (ushort)Math.Sqrt(texture.Length >> 2) : width) << 2),
			ySide = (ushort)((width < 1 ? xSide : texture.Length / width) >> 2);
		x <<= 2;//x *= 4;
		if (x >= xSide || y >= ySide) return texture;
		BinaryPrimitives.WriteUInt32BigEndian(
			destination: texture.AsSpan(
				start: y * xSide + x,
				length: 4),
			value: color);
		return texture;
	}
	#endregion Utilities
}
