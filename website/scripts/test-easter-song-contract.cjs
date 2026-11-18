const fs = require('node:fs');
const path = require('node:path');
const Module = require('node:module');
const ts = require('typescript');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const original = Module._resolveFilename;
Module._resolveFilename = function(request, ...args) { return original.call(this, request.startsWith('@/') ? path.join(root, request.slice(2)) : request, ...args); };
require.extensions['.ts'] = (module, filename) => module._compile(ts.transpileModule(fs.readFileSync(filename, 'utf8'), { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS, esModuleInterop: true } }).outputText, filename);
const { easterSongs } = require('../data/easter-songs.ts');
const { drawSong, newQuestion } = require('../lib/easter-song-games.ts');
const facts = require('../data/song-effect-facts.json');
const catalog = require('../data/song-effects-catalog.json');
assert.equal(easterSongs.length, catalog.entries.length);
for (const entry of catalog.entries) {
  const song = easterSongs.find(s => s.id === entry.id);
  assert.deepEqual(song.definition, entry.effect);
  assert.deepEqual(song.aliases, entry.match.artistAliases);
  assert.equal(song.title, entry.match.titleBase);
  assert.equal(facts[song.signature].length, 4);
}
for (let i = 0; i < easterSongs.length; i++) {
  const q = newQuestion(undefined, () => (i + .1) / easterSongs.length);
  assert.equal(q.answer.id, easterSongs[i].id);
  assert.equal(q.options.length, 3);
  assert.equal(q.options.filter(song => song.signature === q.answer.signature).length, 1);
  assert.notEqual(drawSong(q.answer.id, () => .5).id, q.answer.id);
}
console.log('Passed: all song/effect/alias mappings, four-language facts, unique quiz answers for every song, nonrepeating draws.');
