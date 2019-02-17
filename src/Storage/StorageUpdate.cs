using System.Collections.Generic;

namespace hl18
{
    public partial class Storage
    {
        public int ValidateUpdateAccount(DtoAccount dto)
        {
            var id = dto.id;
            if (id == 17579)
            { }

            // validate first, starting from fastest checks, and only after the validation create an account
            if (id < 0 || id >= MAX_ACCOUNTS)
                return 404;

            // phone (if provided) contains area code 
            if (dto.flags.HasFlag(DtoFlags.Phone) && dto.phone != null)
            {
                var openBrace = dto.phone.IndexOf('(');
                var closeBrace = dto.phone.IndexOf(')');
                if (openBrace < 0 || closeBrace != openBrace + 4)
                    return 400;
            }

            // joined within range
            if (dto.flags.HasFlag(DtoFlags.Joined) && (Utils.TimestampToDate(dto.joined) < MinJoined || Utils.TimestampToDate(dto.joined) > MaxJoined))
                return 400;

            // premium
            if (dto.flags.HasFlag(DtoFlags.Premium))
                if (dto.premium.start > 0 && Utils.TimestampToDate(dto.premium.start) < MinPremium ||
                    dto.premium.finish > 0 && Utils.TimestampToDate(dto.premium.finish) < MinPremium)
                    return 400;

            // the rest requires locking
            bool lockTaken = false;
            try
            {
                updateLock.Enter(ref lockTaken);

                // check if the account exists
                if (!All[id])
                    return 404;

                // likes
                if (dto.flags.HasFlag(DtoFlags.Likes))
                    if (!verifyLikes(dto.likes))
                        return 400;

                var acct = Accounts[id];

                // update email and domain
                if (dto.flags.HasFlag(DtoFlags.Email))
                {
                    if (dto.email.IsEmpty || dto.email.Length > 100)
                        return 400;

                    if (!bufferFromEmail(dto.email, out var intEmail))
                        return 400;

                    // check for duplicates
                    if (Emails.Contains(intEmail) &&
                        !ByteArrayComparer.Instance.Equals(acct.Email, intEmail))
                        return 400; // such email exists and it's not ours

                    // unregister old email
                    Domains[acct.GetDomainIdx()].Exclude(id);
                    Emails.Remove(acct.Email);

                    // store and register new email
                    acct.Email = intEmail;
                    Emails.Add(acct.Email);
                    Domains[acct.GetDomainIdx()].Include(id);
                }

                // store new account info
                Accounts[id] = acct;
            }
            finally
            {
                if (lockTaken)
                    updateLock.Exit();
            }

            return 202;
        }



        public void PostUpdateAccount(DtoAccount dto)
        {
            var id = dto.id;
            if (id == 17579)
            { }

            var acct = Accounts[id];

            // sex
            if (dto.flags.HasFlag(DtoFlags.Sex))
            {
                if (dto.sex)
                    acct.Flags |= Account.Male;
                else
                    acct.Flags &= (byte)~Account.Male;
            }

            // status
            if (dto.flags.HasFlag(DtoFlags.Status))
            {
                bool isFree = dto.status == DtoAccount.STATUS_FREE;
                bool isTaken = dto.status == DtoAccount.STATUS_TAKEN;
                bool isComplicated = dto.status == DtoAccount.STATUS_COMPLICATED;
                // clear old flags
                acct.Flags &= (byte)~(Account.Free | Account.Taken);
                // add new flags
                if (isFree)
                    acct.Flags |= Account.Free;
                else
                if (isTaken)
                    acct.Flags |= Account.Taken;
                else
                    acct.Flags |= (byte)(Account.Free | Account.Taken);
            }

            // birth date 
            if (dto.flags.HasFlag(DtoFlags.Birth))
            {
                // exclude from old range
                if (acct.BirthIdx > 0)
                    BirthYears[acct.BirthIdx].Exclude(id);

                // store new birthday
                acct.Birth = dto.birth;

                // include into new range
                acct.BirthIdx = (byte)BirthYears.GetOrCreateRangeThenInclude(
                    Utils.TimestampToDate(acct.Birth).Year, id).Index;
            }

            // joined date
            if (dto.flags.HasFlag(DtoFlags.Joined))
            {
                // exclude from old range
                if (acct.JoinedIdx > 0)
                    JoinYears[acct.JoinedIdx].Exclude(id);

                // include into new range
                acct.JoinedIdx = (byte)JoinYears.GetOrCreateRangeThenInclude(
                    Utils.TimestampToDate(dto.joined).Year, id).Index;
            }

            // premium
            if (dto.flags.HasFlag(DtoFlags.Premium))
            {
                acct.PStart = dto.premium.start;
                acct.PFinish = dto.premium.finish;

                bool premiumNow = acct.PStart <= Now && acct.PFinish >= Now;
                if (premiumNow)
                    acct.Flags |= Account.Premium;
                else
                    acct.Flags &= (byte)~Account.Premium;
            }


            // fname
            if (dto.flags.HasFlag(DtoFlags.Fname))
            {
                // exclude from old range
                if (acct.FNameIdx > 0)
                    Fnames[acct.FNameIdx].Exclude(id);
                acct.FNameIdx = dto.fnameIdx;
                if( acct.FNameIdx > 0 )
                    Fnames[acct.FNameIdx].Include(id);
            }

            // sname
            if (dto.flags.HasFlag(DtoFlags.Sname))
            {
                // exclude from old range
                if (acct.SNameIdx != 0)
                {
                    var entry = Snames[acct.SNameIdx];
                    Snames2.GetOrCreateRange(entry.AName[0] + (entry.AName[1] << 8)).Exclude(id);
                    Snames2.GetOrCreateRange(entry.AName[0] + (entry.AName[1] << 8) + (entry.AName[2] << 16) + (entry.AName[3] << 24)).Exclude(id);
                }
                acct.SNameIdx = dto.snameIdx;

                if (acct.SNameIdx>0)
                {
                    var entry = Snames[acct.SNameIdx];
                    Snames2.GetOrCreateRangeThenInclude(entry.AName[0] + (entry.AName[1] << 8), id);
                    Snames2.GetOrCreateRangeThenInclude(entry.AName[0] + (entry.AName[1] << 8) + (entry.AName[2] << 16) + (entry.AName[3] << 24), id);
                }
            }

            // phone
            if (dto.flags.HasFlag(DtoFlags.Phone))
            {
                // exclude from old range
                var oldAreaCode = areaCodeFromPhone(new AString(acct.Phone));
                if (!oldAreaCode.IsEmpty)
                    AreaCodes.GetOrCreateRange(oldAreaCode).Exclude(id);

                // update the phone number
                acct.Phone = dto.phone.Buffer;

                // include into new range
                var newAreaCode = areaCodeFromPhone(dto.phone);
                if (!newAreaCode.IsEmpty)
                    AreaCodes.GetOrCreateRange(newAreaCode).Include(id);
            }

            // country
            if (dto.flags.HasFlag(DtoFlags.Country))
            {
                // exclude from old range
                if (acct.CountryIdx > 0)
                    Countries[acct.CountryIdx].Exclude(id);
                acct.CountryIdx = dto.countryIdx;

                // include into new range
                if (acct.CountryIdx>0)
                    Countries[acct.CountryIdx].Include(id);
            }

            // city
            if (dto.flags.HasFlag(DtoFlags.City))
            {
                // exclude from old range
                if (acct.CityIdx > 0)
                    Cities[acct.CityIdx].Exclude(id);
                acct.CityIdx = dto.cityIdx;
                // include into new range
                if (acct.CityIdx>0)
                    Cities[acct.CityIdx].Include(id);
            }

            // interests
            if (dto.flags.HasFlag(DtoFlags.Interests))
            {
                // exclude old interests
                for (int i = 1; i < BitMap96.MAX_BITS; i++)
                    if (acct.InterestMask.IsSet(i))
                        Interests[i].Exclude(id);
                acct.InterestMask = dto.interests;
                // include new interests
                for (int i = 1; i < BitMap96.MAX_BITS; i++)
                    if (acct.InterestMask.IsSet(i))
                        Interests[i].Include(id);
            }

            // unlink old likes
            if (dto.flags.HasFlag(DtoFlags.Likes))
            {
                if (acct.LikesCount > 0)
                    Log.Error("Replacing existing likes on update");
                if (dto.likes != null)
                    addLikes(id, ref acct, dto.likes);
            }

            // uncount old record from cube groups
            updateGroups(ref Accounts[id], false);

            // store new account info
            Accounts[id] = acct;

            // count new record in cube groups
            updateGroups(ref acct, true);
        }


    }
}
