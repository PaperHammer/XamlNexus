// Run once after editing resources/logo/XamlNexus-Gallery-icon.svg:
// npm install --prefix .artifacts/gallery-icon-tools --no-save --package-lock=false @resvg/resvg-js@2.6.2
// node eng/Generate-GalleryIcons.cjs
// Generated assets are checked in; normal .NET builds need no Node dependencies.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const { Resvg } = require(path.join(root, '.artifacts/gallery-icon-tools/node_modules/@resvg/resvg-js'));
const source = fs.readFileSync(path.join(root, 'resources/logo/XamlNexus-Gallery-icon.svg'), 'utf8');
const assets = path.join(root, 'samples/XamlNexus.Gallery/XamlNexus.Gallery.UI/Assets');

function render(width, height, size = Math.min(width, height)) {
    const nested = source.replace(/<svg[^>]+>/, `<svg x="${(width - size) / 2}" y="${(height - size) / 2}" width="${size}" height="${size}" viewBox="0 0 128 128">`);
    return new Resvg(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">${nested}</svg>`).render().asPng();
}

for (const [name, width, height, size] of [
    ['icon-48.png', 48, 48],
    ['LockScreenLogo.scale-200.png', 48, 48],
    ['Square150x150Logo.scale-200.png', 300, 300, 220],
    ['Square44x44Logo.scale-200.png', 88, 88],
    ['Square44x44Logo.targetsize-24_altform-unplated.png', 24, 24],
    ['StoreLogo.png', 50, 50],
    ['Wide310x150Logo.scale-200.png', 620, 300, 220],
    ['SplashScreen.scale-200.png', 1240, 600, 280],
]) {
    fs.writeFileSync(path.join(assets, name), render(width, height, size));
}

// Windows ICO supports PNG frames; include native small sizes to avoid shell scaling.
const sizes = [16, 24, 32, 48, 64, 128, 256];
const frames = sizes.map(size => render(size, size));
const header = Buffer.alloc(6 + sizes.length * 16);
header.writeUInt16LE(1, 2);
header.writeUInt16LE(sizes.length, 4);
let offset = header.length;
sizes.forEach((size, index) => {
    const entry = 6 + index * 16;
    header[entry] = header[entry + 1] = size === 256 ? 0 : size;
    header.writeUInt16LE(1, entry + 4);
    header.writeUInt16LE(32, entry + 6);
    header.writeUInt32LE(frames[index].length, entry + 8);
    header.writeUInt32LE(offset, entry + 12);
    offset += frames[index].length;
});
fs.writeFileSync(path.join(assets, 'xamlnexus.ico'), Buffer.concat([header, ...frames]));
console.log('Generated Gallery PNG assets and multi-resolution ICO.');
