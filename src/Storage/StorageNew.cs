namespace hl18
{
    public partial class Storage
    {
        // called from the loader
        public void InitNewAccout(DtoAccount dto)
        {
            dto.flags = DtoFlags.Init;
            ValidateNewAccount(dto);
            PostNewAccount(dto);
        }

        // called from IO thread, respond as fast as possible
        public int ValidateNewAccount(DtoAccount dto)
        {
            // id
            if (!dto.flags.HasFlag(DtoFlags.Id))
                return 400;

            var id = dto.id;

            if (dto.id == 31631)
            { }

            // phone (if provided) contains area code 
            if (dto.phone!=null)
            {
                var openBrace = dto.phone.IndexOf('(');
                var closeBrace = dto.phone.IndexOf(')');
                if (openBrace < 0 || closeBrace != openBrace + 4)
                    return 400;
            }

            // joined within range
            if (Utils.TimestampToDate(dto.joined) < MinJoined || Utils.TimestampToDate(dto.joined) > MaxJoined)
                return 400;

            // premium
            if (dto.premium.start > 0 && Utils.TimestampToDate(dto.premium.start) < MinPremium ||
                dto.premium.finish > 0 && Utils.TimestampToDate(dto.premium.finish) < MinPremium)
                return 400;

            // check email syntaxis
            if (dto.email.IsEmpty || dto.email.Length > 100)
                return 400;

            // validate email syntax and create the email buffer
            if (!bufferFromEmail(dto.email, out var intEmail))
                return 400;

            // the rest requires locking
            bool lockTaken = false;
            try
            {
                updateLock.Enter(ref lockTaken);

                // is there an account with such id already?
                if (All[id])
                    return 400; // account already exists

                if (!Emails.Add(intEmail))
                    return 400; // must be unique

                // check if liked accounts all exist
                if (dto.flags != DtoFlags.Init && dto.likes != null)
                    if (!verifyLikes(dto.likes))
                        return 400;

                // store the email and include into the domain list
                var acct = Accounts[id];
                acct.Email = intEmail;
                Domains[acct.GetDomainIdx()].Include(id);

                // all is good! mark as existing
                All.Set(id, true);
                Accounts[id] = acct;
            }
            finally
            {
                if (lockTaken)
                    updateLock.Exit();
            }

            return 201;
        }

        // this call is serialized
        public void PostNewAccount(DtoAccount dto)
        {
            var id = dto.id;
            var acct = Accounts[id];

            // sex
            if (dto.sex)
                acct.Flags |= Account.Male;

            // status
            if (dto.status == DtoAccount.STATUS_FREE)
                acct.Flags |= Account.Free;
            else
            if (dto.status == DtoAccount.STATUS_TAKEN)
                acct.Flags |= Account.Taken;
            else
                acct.Flags |= (byte)(Account.Free | Account.Taken);

            // birth date and year
            acct.Birth = dto.birth;
            acct.BirthIdx = (byte)BirthYears.GetOrCreateRangeThenInclude(
                Utils.TimestampToDate(acct.Birth).Year, id).Index;

            // joined
            acct.JoinedIdx = (byte)JoinYears.GetOrCreateRangeThenInclude(
                Utils.TimestampToDate(dto.joined).Year, id).Index;

            // premium
            acct.PStart = dto.premium.start;
            acct.PFinish = dto.premium.finish;
            bool premiumNow = acct.PStart <= Now && acct.PFinish >= Now;
            if (premiumNow)
                acct.Flags |= Account.Premium;

            // fname
            acct.FNameIdx = dto.fnameIdx;
            if (acct.FNameIdx > 0)
                Fnames[acct.FNameIdx].Include(id);

            // sname
            acct.SNameIdx = dto.snameIdx;
            if (acct.SNameIdx>0)
            {
                var entry = Snames[dto.snameIdx];
                entry.Include(id);
                Snames2.GetOrCreateRangeThenInclude(entry.AName[0] + (entry.AName[1] << 8), id);
                Snames2.GetOrCreateRangeThenInclude(entry.AName[0] + (entry.AName[1] << 8) + (entry.AName[2] << 16) + (entry.AName[3] << 24), id);
            }

            // phone
            if (!dto.phone.IsEmpty)
            {
                acct.Phone = dto.phone.Buffer;
                // add to area code index
                var areaCodeStr = areaCodeFromPhone(dto.phone);
                if (!areaCodeStr.IsEmpty)
                    AreaCodes.GetOrCreateRangeThenInclude(areaCodeStr, id);
            }

            // country
            acct.CountryIdx = dto.countryIdx;
            if (acct.CountryIdx > 0)
                Countries[acct.CountryIdx].Include(id);

            // city
            acct.CityIdx = dto.cityIdx;
            if (acct.CityIdx > 0)
                Cities[acct.CityIdx].Include(id);

            // interests
            acct.InterestMask = dto.interests;
            for (int i = 1; i < BitMap96.MAX_BITS; i++)
                if (acct.InterestMask.IsSet(i))
                    Interests[i].Include(id);

            // likes
            if (dto.likes != null)
                addLikes(id, ref acct, dto.likes);

            // update the group hypercube
            updateGroups(ref acct, true);

            // store it
            Accounts[id] = acct;
        }

    }
}
