// IAET Ghidra headless post-script.
//
// Exports the analyzed program as JSON so IAET can attach functions, call graphs, strings and
// (optionally) decompiled pseudocode to its knowledge base. Run through analyzeHeadless:
//
//   analyzeHeadless <projDir> <projName> -import <binary> -deleteProject \
//       -scriptPath <dir containing this file> \
//       -postScript IaetGhidraExport.java <output.json> [decompile=true|false] [maxFunctions=N] [maxPseudocodeChars=N]
//
// The script is read-only with respect to the binary: Ghidra works on its own ephemeral project
// database and never modifies the input file.
//
// @category IAET

import java.io.FileWriter;
import java.io.IOException;
import java.io.Writer;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileOptions;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.data.StringDataInstance;
import ghidra.program.model.listing.Data;
import ghidra.program.model.listing.DataIterator;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;
import ghidra.program.model.symbol.Symbol;
import ghidra.program.model.symbol.SymbolIterator;
import ghidra.program.model.symbol.SymbolType;

public class IaetGhidraExport extends GhidraScript {

    public static final String SCRIPT_VERSION = "1";

    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length < 1) {
            printerr("IaetGhidraExport: output path argument is required");
            return;
        }
        String outputPath = args[0];
        boolean decompile = false;
        int maxFunctions = 2000;
        int maxPseudocodeChars = 20000;
        for (int i = 1; i < args.length; i++) {
            String a = args[i];
            if (a.startsWith("decompile=")) {
                decompile = Boolean.parseBoolean(a.substring("decompile=".length()));
            } else if (a.startsWith("maxFunctions=")) {
                maxFunctions = Integer.parseInt(a.substring("maxFunctions=".length()));
            } else if (a.startsWith("maxPseudocodeChars=")) {
                maxPseudocodeChars = Integer.parseInt(a.substring("maxPseudocodeChars=".length()));
            }
        }

        DecompInterface decomp = null;
        if (decompile) {
            decomp = new DecompInterface();
            decomp.setOptions(new DecompileOptions());
            decomp.toggleCCode(true);
            decomp.toggleSyntaxTree(false);
            if (!decomp.openProgram(currentProgram)) {
                printerr("IaetGhidraExport: decompiler failed to open program: " + decomp.getLastMessage());
                decomp = null;
            }
        }

        try (Writer w = new FileWriter(outputPath, java.nio.charset.StandardCharsets.UTF_8)) {
            w.write("{");
            w.write("\"scriptVersion\":\"" + SCRIPT_VERSION + "\",");
            w.write("\"program\":{");
            w.write("\"name\":" + q(currentProgram.getName()) + ",");
            w.write("\"executableFormat\":" + q(currentProgram.getExecutableFormat()) + ",");
            w.write("\"languageId\":" + q(currentProgram.getLanguageID().getIdAsString()) + ",");
            w.write("\"compilerSpecId\":" + q(currentProgram.getCompilerSpec().getCompilerSpecID().getIdAsString()) + ",");
            w.write("\"imageBase\":" + q("0x" + currentProgram.getImageBase().toString(false)) + ",");
            w.write("\"sha256\":" + q(nullToEmpty(currentProgram.getExecutableSHA256())));
            w.write("},");

            writeMemoryBlocks(w);
            w.write(",");
            writeStrings(w);
            w.write(",");
            writeExternalSymbols(w);
            w.write(",");
            writeFunctions(w, decomp, maxFunctions, maxPseudocodeChars);
            w.write("}");
        } finally {
            if (decomp != null) {
                decomp.dispose();
            }
        }
        println("IaetGhidraExport: wrote " + outputPath);
    }

    private void writeMemoryBlocks(Writer w) throws IOException {
        w.write("\"memoryBlocks\":[");
        boolean first = true;
        for (MemoryBlock b : currentProgram.getMemory().getBlocks()) {
            if (!first) w.write(",");
            first = false;
            w.write("{\"name\":" + q(b.getName())
                + ",\"start\":" + q("0x" + b.getStart().toString(false))
                + ",\"size\":" + b.getSize()
                + ",\"read\":" + b.isRead()
                + ",\"write\":" + b.isWrite()
                + ",\"execute\":" + b.isExecute()
                + ",\"initialized\":" + b.isInitialized() + "}");
        }
        w.write("]");
    }

    private void writeStrings(Writer w) throws IOException {
        w.write("\"strings\":[");
        boolean first = true;
        DataIterator it = currentProgram.getListing().getDefinedData(true);
        int count = 0;
        while (it.hasNext() && !monitor.isCancelled() && count < 50000) {
            Data d = it.next();
            if (!d.hasStringValue()) continue;
            StringDataInstance sdi = StringDataInstance.getStringDataInstance(d);
            String value = sdi.getStringValue();
            if (value == null || value.isEmpty()) continue;
            if (!first) w.write(",");
            first = false;
            count++;
            w.write("{\"address\":" + q("0x" + d.getAddress().toString(false))
                + ",\"value\":" + q(value)
                + ",\"charset\":" + q(nullToEmpty(sdi.getCharsetName())) + "}");
        }
        w.write("]");
    }

    private void writeExternalSymbols(Writer w) throws IOException {
        w.write("\"externals\":[");
        boolean first = true;
        SymbolIterator it = currentProgram.getSymbolTable().getExternalSymbols();
        while (it.hasNext() && !monitor.isCancelled()) {
            Symbol s = it.next();
            if (!first) w.write(",");
            first = false;
            w.write("{\"name\":" + q(s.getName())
                + ",\"library\":" + q(s.getParentNamespace() == null ? "" : s.getParentNamespace().getName())
                + ",\"type\":" + q(s.getSymbolType() == SymbolType.FUNCTION ? "function" : "data") + "}");
        }
        w.write("]");
    }

    private void writeFunctions(Writer w, DecompInterface decomp, int maxFunctions, int maxPseudocodeChars) throws IOException {
        w.write("\"functions\":[");
        boolean first = true;
        int count = 0;
        int total = 0;
        FunctionIterator it = currentProgram.getFunctionManager().getFunctions(true);
        while (it.hasNext() && !monitor.isCancelled()) {
            Function f = it.next();
            total++;
            if (count >= maxFunctions) continue;
            count++;
            if (!first) w.write(",");
            first = false;

            w.write("{\"name\":" + q(f.getName())
                + ",\"entry\":" + q("0x" + f.getEntryPoint().toString(false))
                + ",\"size\":" + f.getBody().getNumAddresses()
                + ",\"isThunk\":" + f.isThunk()
                + ",\"isExternal\":" + f.isExternal()
                + ",\"signature\":" + q(f.getPrototypeString(false, false)));

            w.write(",\"callers\":");
            writeNameList(w, f.getCallingFunctions(monitor));
            w.write(",\"callees\":");
            writeNameList(w, f.getCalledFunctions(monitor));

            w.write(",\"strings\":");
            writeReferencedStrings(w, f);

            if (decomp != null && !f.isExternal() && !f.isThunk()) {
                DecompileResults res = decomp.decompileFunction(f, 60, monitor);
                String code = res != null && res.decompileCompleted() && res.getDecompiledFunction() != null
                    ? res.getDecompiledFunction().getC() : null;
                if (code != null) {
                    if (code.length() > maxPseudocodeChars) {
                        code = code.substring(0, maxPseudocodeChars) + "\n/* ... truncated by IAET (maxPseudocodeChars) ... */";
                    }
                    w.write(",\"pseudocode\":" + q(code));
                } else {
                    w.write(",\"pseudocode\":null,\"decompileError\":" + q(res == null ? "no result" : nullToEmpty(res.getErrorMessage())));
                }
            }
            w.write("}");
        }
        w.write("],");
        w.write("\"functionCount\":" + total + ",");
        w.write("\"functionsExported\":" + count + ",");
        w.write("\"truncated\":" + (total > count));
    }

    private void writeNameList(Writer w, Set<Function> functions) throws IOException {
        w.write("[");
        boolean first = true;
        List<String> names = new ArrayList<>();
        for (Function f : functions) names.add(f.getName());
        java.util.Collections.sort(names);
        for (String n : names) {
            if (!first) w.write(",");
            first = false;
            w.write(q(n));
        }
        w.write("]");
    }

    private void writeReferencedStrings(Writer w, Function f) throws IOException {
        w.write("[");
        boolean first = true;
        Set<String> seen = new HashSet<>();
        ReferenceIterator refs = currentProgram.getReferenceManager().getReferenceIterator(f.getBody().getMinAddress());
        int budget = 20000;
        while (refs.hasNext() && budget-- > 0) {
            Reference r = refs.next();
            Address from = r.getFromAddress();
            if (!f.getBody().contains(from)) {
                if (from.compareTo(f.getBody().getMaxAddress()) > 0) break;
                continue;
            }
            Data d = currentProgram.getListing().getDataAt(r.getToAddress());
            if (d == null || !d.hasStringValue()) continue;
            String value = StringDataInstance.getStringDataInstance(d).getStringValue();
            if (value == null || value.isEmpty() || !seen.add(value)) continue;
            if (seen.size() > 64) break;
            if (!first) w.write(",");
            first = false;
            w.write(q(value));
        }
        w.write("]");
    }

    private static String nullToEmpty(String s) {
        return s == null ? "" : s;
    }

    private static String q(String s) {
        if (s == null) return "null";
        StringBuilder sb = new StringBuilder(s.length() + 2);
        sb.append('"');
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            switch (c) {
                case '"': sb.append("\\\""); break;
                case '\\': sb.append("\\\\"); break;
                case '\n': sb.append("\\n"); break;
                case '\r': sb.append("\\r"); break;
                case '\t': sb.append("\\t"); break;
                default:
                    if (c < 0x20 || c == 0x7F) {
                        sb.append(String.format("\\u%04x", (int) c));
                    } else {
                        sb.append(c);
                    }
            }
        }
        sb.append('"');
        return sb.toString();
    }
}
