import Heading from "@/app/components/Heading";
import { auth } from "@/auth";
import { redirect } from "next/navigation";
import AuctionForm from "../AuctionForm";

export default async function Create() {
  const session = await auth();

  if (!session) {
    redirect('/api/auth/signin?callbackUrl=/auctions/create');
  }

  return (
    <div className="mx-auto max-w-[75%] shadow-lg p-10 bg-white rounded-lg">
      <Heading title="Sell your car!" subtitle="Please enter the details of your car" />
      <AuctionForm />
    </div>
  )
}
