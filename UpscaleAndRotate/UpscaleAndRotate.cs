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
		if (rWidth > ushort.MaxValue)
			throw new OverflowException("Rotated width exceeds maximum allowed size.");
		if (rHeight > ushort.MaxValue)
			throw new OverflowException("Rotated height exceeds maximum allowed size.");
		if (rWidth * rHeight > int.MaxValue >> 2)
			throw new OverflowException("Resulting image would be too large to allocate");
		rotatedWidth = (ushort)rWidth;
		rotatedHeight = (ushort)rHeight;
		ushort halfScaledWidth = (ushort)(scaledWidth >> 1),
			halfScaledHeight = (ushort)(scaledHeight >> 1),
			halfRotatedWidth = (ushort)(rotatedWidth >> 1),
			halfRotatedHeight = (ushort)(rotatedHeight >> 1);
		double offsetX = halfScaledWidth - cos * halfRotatedWidth - sin * halfRotatedHeight,
			offsetY = halfScaledHeight - cos * halfRotatedHeight + sin * halfRotatedWidth;
		//Pre-calculate rotated corners in clockwise order:
		//0 = top-left (0,0)
		//1 = top-right (w,0)
		//2 = bottom-right (w,h)
		//3 = bottom-left (0,h)
		bool isNearZero = absCos < 1e-10 || absSin < 1e-10;
		double[] cornerX = isNearZero ? null : [-halfScaledWidth, halfScaledWidth, halfScaledWidth, -halfScaledWidth],
			cornerY = isNearZero ? null : [-halfScaledHeight, -halfScaledHeight, halfScaledHeight, halfScaledHeight];
		if (!isNearZero)
			for (byte cornerIndex = 0; cornerIndex < 4; cornerIndex++)
			{
				double unrotatedX = cornerX[cornerIndex];
				cornerX[cornerIndex] = unrotatedX * cos - cornerY[cornerIndex] * sin + halfRotatedWidth;
				cornerY[cornerIndex] = unrotatedX * sin + cornerY[cornerIndex] * cos + halfRotatedHeight;
			}
		byte[] rotated = new byte[rotatedWidth * rotatedHeight << 2];
		for (ushort y = 0; y < rotatedHeight; y++)
		{
			ushort startX = 0, endX = (ushort)(rotatedWidth - 1);
			if (!isNearZero)
			{
				double? minX = null, maxX = null;
				for (byte cornerIndex = 0; cornerIndex < 4; cornerIndex++)
				{//Check each edge of the rectangle
					if (Math.Abs(cornerY[cornerIndex] - y) <= 0.5)
					{//If this corner is on this scanline
						minX = minX.HasValue ? Math.Min(minX.Value, cornerX[cornerIndex]) : cornerX[cornerIndex];
						maxX = maxX.HasValue ? Math.Max(maxX.Value, cornerX[cornerIndex]) : cornerX[cornerIndex];
					}
					byte nextCornerIndex = (byte)((cornerIndex + 1) % 4);
					double currentY = cornerY[cornerIndex],
						nextY = cornerY[nextCornerIndex];
					if ((currentY <= y && nextY >= y) || (currentY >= y && nextY <= y))
					{//If the edge crosses this scanline
						if (Math.Abs(nextY - currentY) > 1e-10)
						{//Only calculate intersection if denominator isn't zero
							double t = (y - currentY) / (nextY - currentY);
							double intersectX = cornerX[cornerIndex] + t * (cornerX[nextCornerIndex] - cornerX[cornerIndex]);
							minX = minX.HasValue ? Math.Min(minX.Value, intersectX) : intersectX;
							maxX = maxX.HasValue ? Math.Max(maxX.Value, intersectX) : intersectX;
						}
					}
				}
				if (minX.HasValue) startX = (ushort)Math.Max(0, Math.Floor(minX.Value));
				if (maxX.HasValue) endX = (ushort)Math.Min(rotatedWidth - 1, Math.Ceiling(maxX.Value));
			}
			for (ushort x = startX; x <= endX; x++)
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
	#region Related methods for context
	public static (ushort, ushort) RotatedSize(ushort width, ushort height, double radians = 0d, byte scaleX = 1, byte scaleY = 1)
	{
		if (scaleX < 1) throw new ArgumentOutOfRangeException(nameof(scaleX));
		if (scaleY < 1) throw new ArgumentOutOfRangeException(nameof(scaleY));
		if (width > ushort.MaxValue / scaleX)
			throw new OverflowException("Scaled width exceeds maximum allowed size.");
		if (height > ushort.MaxValue / scaleY)
			throw new OverflowException("Scaled height exceeds maximum allowed size.");
		ushort scaledWidth = (ushort)(width * scaleX),
			scaledHeight = (ushort)(height * scaleY);
		radians %= Tau;
		double absCos = Math.Abs(Math.Cos(radians)),
			absSin = Math.Abs(Math.Sin(radians));
		uint rotatedWidth = (uint)(scaledWidth * absCos + scaledHeight * absSin),
			rotatedHeight = (uint)(scaledWidth * absSin + scaledHeight * absCos);
		if (rotatedWidth > ushort.MaxValue)
			throw new OverflowException("Rotated width exceeds maximum allowed size.");
		if (rotatedHeight > ushort.MaxValue)
			throw new OverflowException("Rotated height exceeds maximum allowed size.");
		return ((ushort)rotatedWidth, (ushort)rotatedHeight);
	}
	public static (int, int) RotatedLocate(ushort width, ushort height, int x, int y, double radians = 0d, byte scaleX = 1, byte scaleY = 1)
	{
		if (scaleX < 1) throw new ArgumentOutOfRangeException(nameof(scaleX));
		if (scaleY < 1) throw new ArgumentOutOfRangeException(nameof(scaleY));
		if (width > ushort.MaxValue / scaleX)
			throw new OverflowException("Scaled width exceeds maximum allowed size.");
		if (height > ushort.MaxValue / scaleY)
			throw new OverflowException("Scaled height exceeds maximum allowed size.");
		radians %= Tau;
		double cos = Math.Cos(radians),
			sin = Math.Sin(radians),
			absCos = Math.Abs(cos),
			absSin = Math.Abs(sin);
		ushort scaledWidth = (ushort)(width * scaleX),
			scaledHeight = (ushort)(height * scaleY),
			rotatedWidth = (ushort)(scaledWidth * absCos + scaledHeight * absSin),
			rotatedHeight = (ushort)(scaledWidth * absSin + scaledHeight * absCos),
			halfRotatedWidth = (ushort)(rotatedWidth >> 1),
			halfRotatedHeight = (ushort)(rotatedHeight >> 1);
		double offsetX = (scaledWidth >> 1) - cos * halfRotatedWidth - sin * halfRotatedHeight,
			offsetY = (scaledHeight >> 1) - cos * halfRotatedHeight + sin * halfRotatedWidth;
		return ((int)(cos * (x * scaleX - offsetX) - sin * (y * scaleY - offsetY)),
			(int)(sin * (x * scaleX - offsetX) + cos * (y * scaleY - offsetY)));
	}
	#endregion Related methods for context
}
