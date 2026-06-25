// Tiny dependency-free reader for exported flat object literals used by the doctor tools. It is deliberately
// lexical rather than regex-only so punctuation inside translation strings (for example ", remember:") can
// never be mistaken for another property key.

function skipQuoted(source, start) {
  const quote = source[start];
  let index = start + 1;
  while (index < source.length) {
    if (source[index] === "\\") index += 2;
    else if (source[index] === quote) return index + 1;
    else index++;
  }
  return source.length;
}

function skipComment(source, start) {
  if (source.startsWith("//", start)) {
    const end = source.indexOf("\n", start + 2);
    return end < 0 ? source.length : end + 1;
  }
  if (source.startsWith("/*", start)) {
    const end = source.indexOf("*/", start + 2);
    return end < 0 ? source.length : end + 2;
  }
  return start;
}

function matchingBrace(source, start) {
  let depth = 0;
  for (let index = start; index < source.length; index++) {
    const char = source[index];
    if (char === '"' || char === "'" || char === "`") {
      index = skipQuoted(source, index) - 1;
      continue;
    }
    const afterComment = skipComment(source, index);
    if (afterComment !== index) {
      index = afterComment - 1;
      continue;
    }
    if (char === "{") depth++;
    else if (char === "}" && --depth === 0) return index;
  }
  return -1;
}

function keyToken(source, start) {
  const char = source[start];
  if (char === '"' || char === "'") {
    const end = skipQuoted(source, start);
    return { key: source.slice(start + 1, end - 1).replace(/\\(["'\\])/g, "$1"), end };
  }
  const match = /^[A-Za-z_$][\w$]*/.exec(source.slice(start));
  return match ? { key: match[0], end: start + match[0].length } : null;
}

export function topLevelObjectKeys(source) {
  const keys = new Set();
  let depth = 0;
  let expectsKey = false;
  for (let index = 0; index < source.length; index++) {
    const char = source[index];
    if (char === '"' || char === "'" || char === "`") {
      if (depth === 1 && expectsKey) {
        const token = keyToken(source, index);
        let cursor = token.end;
        while (/\s/.test(source[cursor] ?? "")) cursor++;
        if (source[cursor] === ":") {
          keys.add(token.key);
          expectsKey = false;
        }
        index = token.end - 1;
      } else {
        index = skipQuoted(source, index) - 1;
      }
      continue;
    }
    const afterComment = skipComment(source, index);
    if (afterComment !== index) {
      index = afterComment - 1;
      continue;
    }
    if (char === "{") {
      depth++;
      if (depth === 1) expectsKey = true;
      continue;
    }
    if (char === "}") {
      depth--;
      continue;
    }
    if (depth !== 1) continue;
    if (char === ",") {
      expectsKey = true;
      continue;
    }
    if (!expectsKey || /\s/.test(char)) continue;
    const token = keyToken(source, index);
    if (!token) {
      expectsKey = false;
      continue;
    }
    let cursor = token.end;
    while (/\s/.test(source[cursor] ?? "")) cursor++;
    if (source[cursor] === ":") keys.add(token.key);
    expectsKey = false;
    index = token.end - 1;
  }
  return keys;
}

export function exportedObjectLiterals(source) {
  const objects = [];
  const declaration = /export\s+const\s+(\w+)\s*=\s*\{/g;
  for (const match of source.matchAll(declaration)) {
    const start = match.index + match[0].lastIndexOf("{");
    const end = matchingBrace(source, start);
    if (end >= 0)
      objects.push({ name: match[1], source: source.slice(start, end + 1) });
  }
  return objects;
}
