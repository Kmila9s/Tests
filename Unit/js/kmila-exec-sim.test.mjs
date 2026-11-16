// Unit tests for the Scratch-style exercise interpreter (wwwroot/js/kmila-exec-sim.js).
// Run: node --test Tests/Unit/js/kmila-exec-sim.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import vm from "node:vm";

const HERE = dirname(fileURLToPath(import.meta.url));
const SRC = readFileSync(join(HERE, "../../../App/Kmila/Kmila.Shared/wwwroot/js/kmila-exec-sim.js"), "utf8");

function load() {
  const defined = {};
  const store = {};
  const window = { kmilaBlocksDefine: (t, d) => { defined[t] = d; } };
  const ctx = {
    window, console,
    document: { documentElement: { getAttribute: () => "en" }, addEventListener() {}, querySelectorAll: () => [] },
    localStorage: { getItem: (k) => store[k] ?? null, setItem: (k, v) => { store[k] = v; } },
  };
  vm.createContext(ctx);
  vm.runInContext(SRC, ctx);
  return { sim: window.kmilaExecSim, text: window.kmilaBlockText, defined };
}
const { sim, text, defined } = load();
const run = (world, ws) => JSON.parse(sim.run(world, "{}", JSON.stringify(ws), "en"));

// --- tiny workspace builders (Blockly save() shape) ---
let n = 0;
const blk = (type, fields = {}, inputs = {}) => ({ type, id: "b" + n++, fields, inputs: Object.fromEntries(Object.entries(inputs).map(([k, v]) => [k, { block: v }])) });
const stack = (...bs) => { for (let i = 0; i < bs.length - 1; i++) bs[i].next = { block: bs[i + 1] }; return bs[0]; };
const ws = (...tops) => ({ blocks: { blocks: tops.map((b, i) => ({ ...b, x: 0, y: i * 100 })) } });
const step = (s) => blk("kmila_recipe_step", { STEP: s });

test("new block types are defined and labels translate without emoji", () => {
  for (const t of ["kmila_rs_repeat", "kmila_rs_take", "kmila_rs_wait", "kmila_rs_serve", "kmila_rs_bell", "kmila_sig_assign", "kmila_sig_op", "kmila_sig_ref"]) assert.ok(defined[t], t);
  assert.equal(text("🍕 Knead the dough"), "Knead the dough");
  assert.equal(text("XOR"), "XOR");
});

test("recipe: classic order serves the pizza with 3 stars", () => {
  const r = run("recipe", ws(stack(...["knead", "sauce", "cheese", "bake", "serve"].map(step))));
  assert.equal(r.ok, true); assert.equal(r.stars, 3);
  assert.equal(r.timeline.steps.length, 7);
});
test("recipe: wrong order fails and explains the missing prerequisite", () => {
  const r = run("recipe", ws(stack(...["knead", "cheese", "sauce", "bake", "serve"].map(step))));
  assert.equal(r.ok, false);
  assert.match(r.timeline.steps[2].note.en, /can't "Add the cheese" yet/);
  assert.match(r.timeline.steps[2].note.es, /aún no puedes/);
});
test("recipe: missing serve fails", () => {
  const r = run("recipe", ws(stack(...["knead", "sauce", "cheese", "bake"].map(step))));
  assert.equal(r.ok, false); assert.match(r.verdict.en, /missing: Slice and serve/);
});
test("empty workspace is reported, not crashed", () => {
  const r = run("recipe", ws());
  assert.equal(r.ok, false); assert.match(r.verdict.en, /Add some blocks/);
});

test("kitchen: four dishes run in parallel, total = longest task", () => {
  const r = run("kitchen", ws(stack(...["chop", "marinate", "fry", "boil"].map(step))));
  assert.equal(r.ok, true); assert.equal(r.stars, 3);
  assert.match(r.verdict.en, /after 10 min .* 23 min/);
  assert.deepEqual(r.timeline.steps[1].highlightLines, [1, 2, 3, 4]);
});

const sigIn = (nm) => blk("kmila_signal_in", { NAME: nm });
const gate = (t, l, r) => blk("kmila_gate_" + t, {}, { LHS: l, RHS: r });
const assign = (target, e) => blk("kmila_output_assign", { TARGET: target }, { VALUE: e });
test("logic: half-adder passes all four input combinations", () => {
  const r = run("logic", ws(stack(assign("sum", gate("xor", sigIn("a"), sigIn("b"))), assign("cout", gate("and", sigIn("a"), sigIn("b"))))));
  assert.equal(r.ok, true); assert.equal(r.stars, 3);
  assert.match(r.timeline.code, /sum  <= a xor b;/);
});
test("logic: OR for cout is caught with the failing input", () => {
  const r = run("logic", ws(stack(assign("sum", gate("xor", sigIn("a"), sigIn("b"))), assign("cout", gate("or", sigIn("a"), sigIn("b"))))));
  assert.equal(r.ok, false); assert.match(r.verdict.en, /cout is wrong for a = 0, b = 1/);
});
test("logic: empty gate slot is reported", () => {
  const r = run("logic", ws(assign("sum", gate("xor", sigIn("a"), null))));
  assert.equal(r.ok, false); assert.match(r.verdict.en, /empty slot/);
});

const onClock = (...body) => blk("kmila_on_clock", {}, body.length ? { BODY: stack(...body) } : {});
const inc = () => blk("kmila_increment_count");
test("clocked: one increment per edge counts to 4", () => {
  const r = run("clocked", ws(onClock(inc())));
  assert.equal(r.ok, true); assert.equal(r.stars, 3);
  assert.equal(r.timeline.steps.at(-1).signals.count, "4");
});
test("clocked: two increments still count by one (last assignment wins)", () => {
  const r = run("clocked", ws(onClock(inc(), inc())));
  assert.equal(r.ok, true); assert.equal(r.stars, 2);
  assert.equal(r.timeline.steps.at(-1).signals.count, "4");
  assert.match(r.verdict.en, /last assignment wins/);
});
test("clocked: increment without a clock is a combinational loop", () => {
  const r = run("clocked", ws(inc()));
  assert.equal(r.ok, false); assert.match(r.verdict.en, /combinational loop/);
});

const rs = (t) => blk("kmila_rs_" + t);
const rep = (...body) => blk("kmila_rs_repeat", {}, { BODY: stack(...body) });
const close = (a, b, eps = 0.15) => assert.ok(Math.abs(a - b) < eps, `${a} ≈ ${b}`);
test("restaurant: sequential program ≈ 70.6 s, 1 star (same numbers as the figure's model)", () => {
  const r = run("restaurant", ws(rep(rs("take"), rs("wait"), rs("serve"))));
  assert.equal(r.ok, true); close(r.plan.total, 70.6); assert.equal(r.stars, 1);
});
test("restaurant: polling program ≈ 37.4 s, 2 stars", () => {
  const r = run("restaurant", ws(rep(rs("serve"), rs("take"))));
  assert.equal(r.ok, true); close(r.plan.total, 37.4); assert.equal(r.stars, 2); assert.ok(r.plan.checks > 0);
});
test("restaurant: bells program ≈ 22.7 s, 3 stars, no wasted checks", () => {
  const r = run("restaurant", ws(stack(rep(rs("take")), rep(rs("bell")))));
  assert.equal(r.ok, true); close(r.plan.total, 22.7); assert.equal(r.stars, 3); assert.equal(r.plan.checks, 0);
});
test("restaurant: serving without taking orders leaves every table unserved", () => {
  const r = run("restaurant", ws(rep(rs("serve"))));
  assert.equal(r.ok, false); assert.match(r.verdict.en, /10 table\(s\) were never served/);
});

const sref = (nm) => blk("kmila_sig_ref", { NAME: nm });
const sop = (op, l, r) => blk("kmila_sig_op", { OP: op }, { LHS: l, RHS: r });
const sas = (t, e) => blk("kmila_sig_assign", { TARGET: t }, { VALUE: e });
test("signals: variable chain reaches 16", () => {
  const r = run("signals", ws(stack(sas("tmp", sop("+", sref("a"), sref("b"))), sas("tmp", sop("*", sref("tmp"), sref("2"))), sas("result", sop("+", sref("tmp"), sref("tmp")))))); // 8, 16, 32?
  // tmp = 8, tmp = 16, result = 32 → not the goal; a correct program follows:
  assert.equal(r.ok, false);
  const ok = run("signals", ws(stack(sas("tmp", sop("+", sref("a"), sref("b"))), sas("result", sop("*", sref("tmp"), sref("2"))))));
  assert.equal(ok.ok, true); assert.equal(ok.stars, 3);
});
test("signals: using a signal for the intermediate value reads its OLD value", () => {
  const r = run("signals", ws(stack(sas("s", sop("+", sref("a"), sref("b"))), sas("result", sop("*", sref("s"), sref("2"))))));
  assert.equal(r.ok, false); assert.match(r.verdict.en, /read s before its new value arrived/);
  assert.equal(r.timeline.steps.at(-1).signals.result, "0");
});

test("code(): live listing for each world", () => {
  assert.match(sim.code("logic", JSON.stringify(ws(assign("sum", gate("xor", sigIn("a"), sigIn("b"))))), "en"), /sum  <= a xor b;/);
  assert.match(sim.code("restaurant", JSON.stringify(ws(rep(rs("take")))), "es"), /repetir mientras haya trabajo:\n  tomar la siguiente orden/);
  assert.equal(sim.code("signals", JSON.stringify(ws()), "en"), "");
});
test("stars(): keeps the best", () => {
  assert.equal(sim.stars("x", 2), 2); assert.equal(sim.stars("x", 1), 2); assert.equal(sim.stars("x"), 2); assert.equal(sim.stars("x", 3), 3);
});
