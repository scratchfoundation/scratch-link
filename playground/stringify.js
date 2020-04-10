function getInterestingPropNames (o) {
    if (o instanceof Error) {
        return ['stack'];
    } else if (o instanceof Event) {
        return ['type', 'target'];
    }
    const props = [];
    for (const prop in o) {
        // if (!o.hasOwnProperty(prop)) continue;
        if (typeof o[prop] === 'function') continue;
        if (o.constructor.hasOwnProperty(prop)) continue;

        props.push(prop);
    }
    return props;
}

function sanitizeString(s) {
    const map = {
        '\n': '\u21B5', // "Downwards Arrow with Corner Leftwards"
        //'\t': '    ',
        //'\t': '\u21E5', // "Rightwards Arrow to Bar"
    };
    const regexp = new RegExp(`[${Object.keys(map).join('')}]`);
    return s.replace(regexp, c => map[c]);
}

function stringify(o, depth=4) {
    if (depth < 1) return '\u2026'; // "Horizontal Ellipsis"
    switch (typeof o) {
    case 'function':
        return "\u0192"; // "Latin Small Letter F with Hook"
    case 'object':
        const props = getInterestingPropNames(o);
        const parts = [];
        if (depth > 1) {
            for (const prop of props) {
                // no filtering necessary here thanks to getInterestingPropNames
                parts.push(`${prop}: ${stringify(o[prop], depth-1)}`);
            }
        } else {
            parts.push('\u2026'); // "Horizontal Ellipsis"
        }
        return `${o.constructor.name} {${parts.join(', ')}}`;
    case 'string':
        return `"${sanitizeString(o)}"`;
    default:
        return o.toString();
    }
}
