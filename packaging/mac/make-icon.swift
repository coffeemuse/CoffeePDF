// Draws the sizes of CoffeePDF.app's icon for make-icon.sh, which packs them into CoffeePDF.icns.
//
//   swift packaging/mac/make-icon.swift <artwork.png> <out.iconset>
//
// The artwork goes on Apple's grid for a macOS icon: a rounded square 824 pixels across in the middle of a 1024 canvas,
// with a corner radius of 185.4 and continuous corners, casting a soft shadow. SwiftUI's continuous rounded rectangle
// is the curve Apple's own icons use. Each size is drawn from the artwork, not scaled down from the largest.
import AppKit
import SwiftUI
import UniformTypeIdentifiers

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data("make-icon.swift: \(message)\n".utf8))
    exit(1)
}

let arguments = CommandLine.arguments
guard arguments.count == 3 else { fail("usage: make-icon.swift <artwork.png> <out.iconset>") }
let iconset = URL(fileURLWithPath: arguments[2])
guard let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: arguments[1]) as CFURL, nil),
      let art = CGImageSourceCreateImageAtIndex(source, 0, nil)
else { fail("can't read \(arguments[1])") }
guard art.width == art.height, art.width >= 1024 else {
    fail("the artwork is \(art.width)x\(art.height); it must be square and at least 1024 pixels across")
}

let sRGB = CGColorSpace(name: CGColorSpace.sRGB)!
let plate = CGRect(x: 100, y: 100, width: 824, height: 824)
let shape = RoundedRectangle(cornerRadius: 185.4, style: .continuous).path(in: plate).cgPath

// The icon at `pixels` across, drawn in the 1024 grid's coordinates.
func draw(_ pixels: Int) -> CGImage {
    let scale = CGFloat(pixels) / 1024
    guard let context = CGContext(data: nil, width: pixels, height: pixels, bitsPerComponent: 8, bytesPerRow: 0,
                                  space: sRGB, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
    else { fail("can't make a \(pixels)-pixel canvas") }
    context.interpolationQuality = .high
    context.scaleBy(x: scale, y: scale)
    // Apple's template's shadow: 10 down, blurred 10, black at 30%. A shadow ignores the scale, so it's scaled here.
    context.setShadow(offset: CGSize(width: 0, height: -10 * scale), blur: 10 * scale,
                      color: CGColor(gray: 0, alpha: 0.3))
    // Clipped in a layer, the artwork casts the shadow as one shape.
    context.beginTransparencyLayer(auxiliaryInfo: nil)
    context.addPath(shape)
    context.clip()
    context.draw(art, in: plate)
    context.endTransparencyLayer()
    guard let image = context.makeImage() else { fail("can't draw the \(pixels)-pixel icon") }
    return image
}

func write(_ image: CGImage, to url: URL, dpi: Int) {
    guard let destination = CGImageDestinationCreateWithURL(url as CFURL, UTType.png.identifier as CFString, 1, nil)
    else { fail("can't write \(url.path)") }
    let properties = [kCGImagePropertyDPIWidth: dpi, kCGImagePropertyDPIHeight: dpi] as CFDictionary
    CGImageDestinationAddImage(destination, image, properties)
    guard CGImageDestinationFinalize(destination) else { fail("can't write \(url.path)") }
}

// iconutil's names: each size at 1x and 2x, from 16 to 512 points.
do {
    try FileManager.default.createDirectory(at: iconset, withIntermediateDirectories: true)
} catch {
    fail("can't make \(iconset.path): \(error.localizedDescription)")
}
for points in [16, 32, 128, 256, 512] {
    for scale in [1, 2] {
        let name = "icon_\(points)x\(points)\(scale == 2 ? "@2x" : "").png"
        write(draw(points * scale), to: iconset.appendingPathComponent(name), dpi: 72 * scale)
    }
}
