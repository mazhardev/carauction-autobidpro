'use client';

import { Button, Spinner } from "flowbite-react";
import { usePathname, useRouter } from "next/navigation";
import { FieldValues, useForm } from "react-hook-form";
import Input from "../components/Input";
import { useEffect } from "react";
import DateInput from "../components/DateInput";
import { createAuction, updateAuction } from "../actions/auctionActions";
import toast from "react-hot-toast";
import { Auction } from "@/types";

type Props = {
    auction?: Auction;
}

export default function AuctionForm({ auction }: Props) {
    const router = useRouter();
    const pathname = usePathname();
    const maxVehicleYear = new Date().getFullYear() + 1;
    const { control, handleSubmit, setFocus, reset,
        formState: { isSubmitting, isValid, isDirty } } = useForm({
            mode: 'onTouched'
        });

    useEffect(() => {
        if (auction) {
            const { make, model, color, mileage, year } = auction;
            reset({ make, model, color, mileage, year })
        }
        setFocus('make')
    }, [setFocus, auction, reset])

    async function onSubmit(data: FieldValues) {
        try {
            let id = '';
            let res;
            if (pathname === '/auctions/create') {
                res = await createAuction(data);
                id = res.id;
            } else {
                if (auction) {
                    res = await updateAuction(data, auction.id);
                    id = auction.id;
                }
            }
            if (res.error) {
                throw res.error;
            }
            router.push(`/auctions/details/${id}`)
        } catch (error: any) {
            toast.error(error.status ? `${error.status} ${error.message}` : 'Something went wrong')
        }
    }

    return (
        <form className="flex flex-col mt-3" onSubmit={handleSubmit(onSubmit)}>
            <Input name="make" label="Make" control={control}
                rules={{ required: 'Make is required' }} />
            <Input name="model" label="Model" control={control}
                rules={{ required: 'Model is required' }} />
            <Input name="color" label="Color" control={control}
                rules={{ required: 'Color is required' }} />

            <div className="grid grid-cols-2 gap-3">
                <Input name="year" label="Year" type='number' control={control}
                    rules={{
                        required: 'Year is required',
                        min: { value: 1886, message: 'Year must be 1886 or later' },
                        max: { value: maxVehicleYear, message: `Year cannot be later than ${maxVehicleYear}` }
                    }} />
                <Input name="mileage" label="Mileage" type='number' control={control}
                    rules={{
                        required: 'Mileage is required',
                        min: { value: 0, message: 'Mileage cannot be negative' }
                    }} />
            </div>

            {pathname === '/auctions/create' &&
            <>
                <Input name="imageUrl" label="Image URL" control={control}
                    rules={{
                        required: 'Image URL is required',
                        validate: (value: string) => {
                            try {
                                new URL(value);
                                return true;
                            } catch {
                                return 'Enter a valid image URL';
                            }
                        }
                    }} />

                <div className="grid grid-cols-2 gap-3">
                    <Input name="reservePrice" label="Reserve price (enter 0 if no reserve)"
                        type='number' control={control}
                        rules={{
                            required: 'Reserve price is required',
                            min: { value: 0, message: 'Reserve price cannot be negative' }
                        }} />
                    <DateInput
                        name="auctionEnd"
                        label="Auction end date/time"
                        control={control}
                        showTimeSelect
                        minDate={new Date()}
                        dateFormat='dd MMMM yyyy h:mm a'
                        rules={{
                            required: 'Auction end date is required',
                            validate: (value: Date | null) =>
                                value && value > new Date() ? true : 'Auction end must be in the future'
                        }}
                    />
                </div>
            </>}


            <div className="flex justify-between">
                <Button type="button" color='alternative' onClick={() => router.push('/')}>Cancel</Button>
                <Button
                    outline
                    color='green'
                    type="submit"
                    disabled={isSubmitting || !isValid || !isDirty}
                >
                    {isSubmitting && <Spinner size="sm" />}
                    Submit
                </Button>
            </div>
        </form>
    )
}
