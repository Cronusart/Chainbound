import { expect } from "chai";
import { readFileSync } from "fs";
import { resolve } from "path";

describe("chainbound_game MagicBlock VRF IDL", () => {
  const idl = JSON.parse(readFileSync(resolve("target/idl/chainbound_game.json"), "utf8"));
  const instruction = (name: string): any => {
    const value = idl.instructions.find((candidate: any) => candidate.name === name);
    expect(value, `missing ${name}`).not.to.equal(undefined);
    return value;
  };

  it("pins the base-layer program, queue, callback signer, and owner consume signer", () => {
    const request = instruction("request_encounter");
    const callback = instruction("fulfill_encounter");
    const consume = instruction("consume_encounter_randomness");

    expect(request.accounts.map((account: any) => account.name)).to.deep.equal([
      "encounter_request", "payer", "oracle_queue", "program_identity",
      "vrf_program", "slot_hashes", "system_program",
    ]);
    expect(request.accounts.find((account: any) => account.name === "oracle_queue").address)
      .to.equal("Cuj97ggrhhidhbu39TijNVqE74xvKJ69gDervRUXAxGh");
    expect(request.accounts.find((account: any) => account.name === "vrf_program").address)
      .to.equal("Vrf1RNUjXmQGjmQrQLvJHs9SNkvDJEsRVFPkfSQUwGz");
    expect(callback.accounts[0]).to.include({ name: "vrf_program_identity", signer: true });
    expect(consume.accounts[1]).to.include({ name: "owner", signer: true });
  });

  it("publishes EncounterRequest and consume-once instructions", () => {
    expect(idl.accounts.some((account: any) => account.name === "EncounterRequest")).to.equal(true);
    expect(instruction("consume_encounter_randomness").discriminator).to.have.length(8);
  });
});
