import * as anchor from "@coral-xyz/anchor";
import { expect } from "chai";

describe("chainbound_game authority boundary", () => {
  anchor.setProvider(anchor.AnchorProvider.env());

  const provider = anchor.getProvider() as anchor.AnchorProvider;
  const program: any = anchor.workspace.chainboundGame;
  const resultAuthority = anchor.web3.Keypair.generate();
  const player = anchor.web3.Keypair.generate();
  const [config] = anchor.web3.PublicKey.findProgramAddressSync(
    [Buffer.from("game_config")],
    program.programId,
  );

  before(async () => {
    const signature = await provider.connection.requestAirdrop(
      resultAuthority.publicKey,
      anchor.web3.LAMPORTS_PER_SOL,
    );
    await provider.connection.confirmTransaction(signature, "confirmed");

    await program.methods
      .initialize(resultAuthority.publicKey, 1, 100, 10, new anchor.BN(0))
      .accounts({
        config,
        programAuthority: provider.wallet.publicKey,
        systemProgram: anchor.web3.SystemProgram.programId,
      })
      .rpc();
  });

  it("creates exactly one singleton config with the server result authority", async () => {
    const stored = await program.account.gameConfig.fetch(config);
    expect(stored.programAuthority.toBase58()).to.equal(provider.wallet.publicKey.toBase58());
    expect(stored.resultAuthority.toBase58()).to.equal(resultAuthority.publicKey.toBase58());
    expect(stored.schemaVersion).to.equal(1);

    await expectFailure(
      program.methods
        .initialize(resultAuthority.publicKey, 1, 100, 10, new anchor.BN(0))
        .accounts({ config, programAuthority: provider.wallet.publicKey, systemProgram: anchor.web3.SystemProgram.programId })
        .rpc(),
    );
  });

  it("rejects a player signer on a result-authority instruction", async () => {
    await expectFailure(
      program.methods
        .applyXpAndMaybeLevelUp(1, 1)
        .accounts({ config, resultAuthority: player.publicKey })
        .signers([player])
        .rpc(),
      "Invalid authority",
    );
  });

  it("rejects authority instructions when the config PDA is missing or spoofed", async () => {
    await expectFailure(
      program.methods
        .applyXpAndMaybeLevelUp(1, 1)
        .accounts({ config: anchor.web3.Keypair.generate().publicKey, resultAuthority: resultAuthority.publicKey })
        .signers([resultAuthority])
        .rpc(),
    );
  });

  it("publishes the scoped MagicBlock request, callback, and consume ABI", () => {
    const instruction = (name: string) => {
      const value = program.idl.instructions.find((candidate: any) => candidate.name === name);
      expect(value, `missing ${name} in generated IDL`).not.to.equal(undefined);
      return value!;
    };

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
    expect(callback.accounts.map((account: any) => account.name)).to.deep.equal([
      "vrf_program_identity", "encounter_request",
    ]);
    expect(callback.accounts[0].signer).to.equal(true);
    expect(consume.accounts.map((account: any) => account.name)).to.deep.equal([
      "encounter_request", "owner",
    ]);
    expect(consume.accounts[1].signer).to.equal(true);
  });
});

async function expectFailure(promise: Promise<unknown>, message?: string): Promise<void> {
  try {
    await promise;
    expect.fail("Expected transaction to fail");
  } catch (error) {
    if (message) expect(String(error)).to.contain(message);
  }
}
