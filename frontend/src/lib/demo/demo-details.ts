export interface DemoDetails {
  legalName: string;
  nric: string;
  mobile: string;
  residentialAddress: string;
}

export type RandomSource = () => number;

const givenNames = [
  "Aiman",
  "Farah",
  "Daniel",
  "Mei Ling",
  "Priya",
  "Hafiz",
  "Siew Mun",
  "Kavitha",
];
const familyNames = ["Demo", "Contoh", "Sample", "Ujian", "Specimen", "Fiksyen"];

const streets = [
  "Jalan Contoh 3/4",
  "Jalan Demo 7",
  "Lorong Ujian 2",
  "Jalan Fiksyen 1/9",
  "Persiaran Sample 5",
];

const areas = [
  { area: "Taman Contoh", postcode: "47400", city: "Petaling Jaya", state: "Selangor" },
  { area: "Taman Demo", postcode: "51200", city: "Kuala Lumpur", state: "Wilayah Persekutuan" },
  { area: "Taman Fiksyen", postcode: "11900", city: "Bayan Lepas", state: "Pulau Pinang" },
];

function pick<T>(items: readonly T[], random: RandomSource): T {
  return items[Math.floor(random() * items.length)];
}

function integerBetween(minimum: number, maximum: number, random: RandomSource) {
  return minimum + Math.floor(random() * (maximum - minimum + 1));
}

function digits(value: number, length: number) {
  return String(value).padStart(length, "0");
}

// The backend only checks for twelve digits. A month of 00 or 13 can never be a real birth date, so the
// result cannot be an issued NRIC, although no reserved range is claimed.
export function demoNric(random: RandomSource) {
  const year = integerBetween(0, 99, random);
  const month = random() < 0.5 ? "00" : "13";
  const day = integerBetween(1, 28, random);
  const placeCode = integerBetween(1, 59, random);
  const serial = integerBetween(0, 9999, random);
  return `${digits(year, 2)}${month}${digits(day, 2)}${digits(placeCode, 2)}${digits(serial, 4)}`;
}

export function demoDetails(random: RandomSource = Math.random): DemoDetails {
  const place = pick(areas, random);
  const houseNumber = integerBetween(1, 60, random);
  return {
    legalName: `${pick(givenNames, random)} ${pick(familyNames, random)}`,
    nric: demoNric(random),
    mobile: `01${integerBetween(0, 9, random)}-${digits(integerBetween(0, 9999999, random), 7)}`,
    residentialAddress: `${houseNumber}, ${pick(streets, random)}, ${place.area}, ${place.postcode} ${place.city}, ${place.state}`,
  };
}
